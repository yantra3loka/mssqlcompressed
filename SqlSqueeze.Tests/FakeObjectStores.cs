// In-memory stand-ins for the parts of S3 and Azure Blob that the storage tests touch.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSqueeze.Tests
{
    /// <summary>An object store keyed by URL path, with the bits of S3 and Azure these tests touch.</summary>
    internal abstract class FakeStore : HttpMessageHandler
    {
        public readonly Dictionary<string, byte[]> Objects = new Dictionary<string, byte[]>();
        public readonly Dictionary<string, DateTime> Modified = new Dictionary<string, DateTime>();
        public readonly List<string> Deleted = new List<string>();

        public void Add(string path, string text, DateTime modifiedUtc)
        {
            Objects[path] = Encoding.UTF8.GetBytes(text);
            Modified[path] = modifiedUtc;
        }

        protected static HttpResponseMessage Status(HttpStatusCode code, string body = null)
        {
            HttpResponseMessage r = new HttpResponseMessage(code);
            if (body != null)
            {
                r.Content = new StringContent(body);
            }
            return r;
        }

        protected static byte[] Body(HttpRequestMessage request)
        {
            return request.Content == null ? new byte[0] : request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }
    }

    internal sealed class FakeS3 : FakeStore
    {
        public readonly List<string> Completed = new List<string>();
        public readonly List<string> AbortedUploads = new List<string>();
        public int FailPartNumber;   // answer this part number with a 500
        private readonly Dictionary<string, SortedDictionary<int, byte[]>> mParts = new Dictionary<string, SortedDictionary<int, byte[]>>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
            string query = request.RequestUri.Query;
            string method = request.Method.Method;
            HttpResponseMessage r;

            if (method == "HEAD" && path.TrimEnd('/').Count(c => c == '/') == 1)   // HEAD on the bucket itself
            {
                r = Status(HttpStatusCode.OK);
            }
            else if (method == "POST" && query.Contains("uploads"))
            {
                mParts[path] = new SortedDictionary<int, byte[]>();
                r = Status(HttpStatusCode.OK, "<InitiateMultipartUploadResult><UploadId>U1</UploadId></InitiateMultipartUploadResult>");
            }
            else if (method == "PUT" && query.Contains("partNumber"))
            {
                int part = int.Parse(Regex.Match(query, @"partNumber=(\d+)").Groups[1].Value);
                if (FailPartNumber == part)
                {
                    return Task.FromResult(Status(HttpStatusCode.InternalServerError, "boom"));
                }
                mParts[path][part] = Body(request);
                r = Status(HttpStatusCode.OK);
                r.Headers.TryAddWithoutValidation("ETag", "\"E" + part + "\"");
            }
            else if (method == "POST" && query.Contains("uploadId"))
            {
                Objects[path] = mParts[path].Values.SelectMany(b => b).ToArray();
                Modified[path] = DateTime.UtcNow;
                Completed.Add(path);
                r = Status(HttpStatusCode.OK, "<CompleteMultipartUploadResult><ETag>\"DONE\"</ETag></CompleteMultipartUploadResult>");
            }
            else if (method == "GET" && query.Contains("list-type=2"))
            {
                StringBuilder xml = new StringBuilder("<ListBucketResult>");
                string bucketPrefix = "/" + path.Trim('/') + "/";
                foreach (KeyValuePair<string, byte[]> o in Objects.Where(o => o.Key.StartsWith(bucketPrefix)).OrderBy(o => o.Key))
                {
                    xml.Append("<Contents><Key>").Append(o.Key.Substring(bucketPrefix.Length)).Append("</Key><LastModified>")
                       .Append(Modified[o.Key].ToString("o")).Append("</LastModified></Contents>");
                }
                xml.Append("<NextContinuationToken /></ListBucketResult>");
                r = Status(HttpStatusCode.OK, xml.ToString());
            }
            else if (method == "GET")
            {
                r = Objects.ContainsKey(path)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Objects[path]) }
                    : Status(HttpStatusCode.NotFound, "<Error><Code>NoSuchKey</Code></Error>");
            }
            else if (method == "DELETE" && query.Contains("uploadId"))
            {
                AbortedUploads.Add(path);
                mParts.Remove(path);
                r = Status(HttpStatusCode.NoContent);
            }
            else if (method == "DELETE")
            {
                Deleted.Add(path);
                Objects.Remove(path);
                r = Status(HttpStatusCode.NoContent);
            }
            else
            {
                r = Status(HttpStatusCode.BadRequest, "unexpected " + method + " " + path);
            }
            return Task.FromResult(r);
        }
    }

    internal sealed class FakeAzure : FakeStore
    {
        public int FailBlockNumber;   // answer the Nth staged block with a 500
        public int StagedBlocks { get { return mBlocksSeen; } }
        private int mBlocksSeen;
        private readonly Dictionary<string, Dictionary<string, byte[]>> mBlocks = new Dictionary<string, Dictionary<string, byte[]>>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
            string query = Uri.UnescapeDataString(request.RequestUri.Query);
            string method = request.Method.Method;
            HttpResponseMessage r;

            if (method == "PUT" && query.Contains("restype=container"))
            {
                r = Status(HttpStatusCode.Created);
            }
            else if (method == "PUT" && query.Contains("comp=blocklist"))
            {
                string xml = Encoding.UTF8.GetString(Body(request));
                List<string> ids = Regex.Matches(xml, "<Latest>([^<]+)</Latest>").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                Objects[path] = ids.SelectMany(id => mBlocks[path][id]).ToArray();
                Modified[path] = DateTime.UtcNow;
                r = Status(HttpStatusCode.Created);
            }
            else if (method == "PUT" && query.Contains("comp=block"))
            {
                mBlocksSeen++;
                if (FailBlockNumber == mBlocksSeen)
                {
                    return Task.FromResult(Status(HttpStatusCode.InternalServerError, "boom"));
                }
                string id = Regex.Match(query, @"blockid=([^&]+)").Groups[1].Value;
                if (!mBlocks.ContainsKey(path))
                {
                    mBlocks[path] = new Dictionary<string, byte[]>();
                }
                mBlocks[path][id] = Body(request);
                r = Status(HttpStatusCode.Created);
            }
            else if (method == "GET" && query.Contains("comp=list"))
            {
                StringBuilder xml = new StringBuilder("<EnumerationResults><Blobs>");
                string containerPrefix = "/" + path.Trim('/') + "/";
                foreach (KeyValuePair<string, byte[]> o in Objects.Where(o => o.Key.StartsWith(containerPrefix)).OrderBy(o => o.Key))
                {
                    xml.Append("<Blob><Name>").Append(o.Key.Substring(containerPrefix.Length)).Append("</Name><Properties><Last-Modified>")
                       .Append(Modified[o.Key].ToString("R")).Append("</Last-Modified></Properties></Blob>");
                }
                xml.Append("</Blobs><NextMarker /></EnumerationResults>");
                r = Status(HttpStatusCode.OK, xml.ToString());
            }
            else if (method == "GET")
            {
                r = Objects.ContainsKey(path)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Objects[path]) }
                    : Status(HttpStatusCode.NotFound, "<Error><Code>BlobNotFound</Code></Error>");
            }
            else if (method == "DELETE")
            {
                Deleted.Add(path);
                Objects.Remove(path);
                r = Status(HttpStatusCode.Accepted);
            }
            else
            {
                r = Status(HttpStatusCode.BadRequest, "unexpected " + method + " " + path);
            }
            return Task.FromResult(r);
        }
    }


    internal sealed class AlwaysStatus : HttpMessageHandler
    {
        private readonly HttpStatusCode mCode;
        public AlwaysStatus(HttpStatusCode code) { mCode = code; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(mCode) { Content = new StringContent("denied") });
        }
    }
}
