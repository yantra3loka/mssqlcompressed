// Manifests for s3 and azure storage: stored as a sibling object next to the
// first backup object, read back, and removed by prune together with their
// backup. The services are faked in memory; the live runs use moto/Azurite.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SqlSqueeze.Core;
using SqlSqueeze.Plugins;
using SqlSqueeze.Plugins.Storage;
using Xunit;

namespace SqlSqueeze.Tests
{
    public class StorageManifestTests
    {
        private static Dictionary<string, List<string>> Config(params string[] pairs)
        {
            var d = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string pair in pairs)
            {
                int eq = pair.IndexOf('=');
                string key = pair.Substring(0, eq);
                if (!d.ContainsKey(key))
                {
                    d[key] = new List<string>();
                }
                d[key].Add(pair.Substring(eq + 1));
            }
            return d;
        }

        /// <summary>An object store keyed by URL path, with the bits of S3 and Azure these tests touch.</summary>
        private abstract class FakeStore : HttpMessageHandler
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

        private sealed class FakeS3 : FakeStore
        {
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
                    mParts[path][part] = Body(request);
                    r = Status(HttpStatusCode.OK);
                    r.Headers.TryAddWithoutValidation("ETag", "\"E" + part + "\"");
                }
                else if (method == "POST" && query.Contains("uploadId"))
                {
                    Objects[path] = mParts[path].Values.SelectMany(b => b).ToArray();
                    Modified[path] = DateTime.UtcNow;
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

        private sealed class FakeAzure : FakeStore
        {
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

        private static readonly string[] S3Base = { "endpoint=http://127.0.0.1:9000", "bucket=bkt", "region=us-east-1", "accesskey=AK", "secretkey=SK" };
        private static readonly string[] AzureBase = { "endpoint=http://127.0.0.1:10000/devstoreaccount1", "container=cont", "account=devstoreaccount1",
            "accountkey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==" };

        private static Dictionary<string, List<string>> S3Config(params string[] more)
        {
            return Config(S3Base.Concat(more).ToArray());
        }

        private static Dictionary<string, List<string>> AzureConfig(params string[] more)
        {
            return Config(AzureBase.Concat(more).ToArray());
        }

        // ---- s3 ----

        [Fact]
        public void S3_WriteManifest_StoresItNextToTheFirstKey_AndReadManifestGetsItBack()
        {
            FakeS3 fake = new FakeS3();
            S3Storage storage = new S3Storage(fake);
            var cfg = S3Config("key=db/app.bak", "key=db/app_2.bak");

            storage.WriteManifest(cfg, "{\"database\":\"app\"}");

            Assert.True(fake.Objects.ContainsKey("/bkt/db/app.bak.manifest.json"), string.Join(", ", fake.Objects.Keys));
            Assert.Equal("{\"database\":\"app\"}", storage.ReadManifest(cfg));
        }

        [Fact]
        public void S3_ReadManifest_IsNullWhenThereIsNone()
        {
            Assert.Null(new S3Storage(new FakeS3()).ReadManifest(S3Config("key=db/none.bak")));
        }

        [Fact]
        public void S3_ReadManifest_AFailureThatIsNotNotFound_IsNotHiddenAsNoManifest()
        {
            S3Storage storage = new S3Storage(new AlwaysStatus(HttpStatusCode.Forbidden));

            Assert.ThrowsAny<Exception>(() => storage.ReadManifest(S3Config("key=db/app.bak")));
        }

        [Fact]
        public void S3_GetFileNames_AreTheKeysTheWriterUsed_DateTokensExpanded()
        {
            S3Storage storage = new S3Storage(new FakeS3());
            var cfg = S3Config("key=db/app_{yyyy}.bak");

            storage.GetBackupWriter(cfg);

            IList<string> names = storage.GetFileNames(cfg);
            Assert.Equal("app_" + DateTime.Now.Year + ".bak", names[0]);
        }

        [Fact]
        public void S3_ManifestGoesNextToTheExpandedKey()
        {
            FakeS3 fake = new FakeS3();
            S3Storage storage = new S3Storage(fake);
            var cfg = S3Config("key=db/app_{yyyy}.bak");
            storage.GetBackupWriter(cfg);

            storage.WriteManifest(cfg, "{}");

            Assert.True(fake.Objects.ContainsKey("/bkt/db/app_" + DateTime.Now.Year + ".bak.manifest.json"), string.Join(", ", fake.Objects.Keys));
        }

        [Fact]
        public void S3_Prune_RemovesTheManifestWithItsBackup_AndCountsABackupOnce()
        {
            FakeS3 fake = new FakeS3();
            DateTime old = DateTime.UtcNow.AddDays(-40);
            fake.Add("/bkt/live/old.bak", "data", old);
            fake.Add("/bkt/live/old.bak.manifest.json", "{}", old);
            fake.Add("/bkt/live/new.bak", "data", DateTime.UtcNow);
            fake.Add("/bkt/live/new.bak.manifest.json", "{}", DateTime.UtcNow);

            PruneResult result = new S3Storage(fake).PruneOldFiles(Config(S3Base.Concat(new[] { "prefix=live/", "olderThanDays=30" }).ToArray()));

            Assert.Equal(1, result.DeletedFiles);
            Assert.False(fake.Objects.ContainsKey("/bkt/live/old.bak"));
            Assert.False(fake.Objects.ContainsKey("/bkt/live/old.bak.manifest.json"));
            Assert.True(fake.Objects.ContainsKey("/bkt/live/new.bak"));
            Assert.True(fake.Objects.ContainsKey("/bkt/live/new.bak.manifest.json"));
        }

        // ---- azure ----

        [Fact]
        public void Azure_WriteManifest_StoresItNextToTheFirstBlob_AndReadManifestGetsItBack()
        {
            FakeAzure fake = new FakeAzure();
            AzureStorage storage = new AzureStorage(fake);
            var cfg = AzureConfig("blob=db/app.bak", "blob=db/app_2.bak");

            storage.WriteManifest(cfg, "{\"database\":\"app\"}");

            Assert.True(fake.Objects.ContainsKey("/devstoreaccount1/cont/db/app.bak.manifest.json"), string.Join(", ", fake.Objects.Keys));
            Assert.Equal("{\"database\":\"app\"}", storage.ReadManifest(cfg));
        }

        [Fact]
        public void Azure_ReadManifest_IsNullWhenThereIsNone()
        {
            Assert.Null(new AzureStorage(new FakeAzure()).ReadManifest(AzureConfig("blob=db/none.bak")));
        }

        [Fact]
        public void Azure_ReadManifest_AFailureThatIsNotNotFound_IsNotHiddenAsNoManifest()
        {
            AzureStorage storage = new AzureStorage(new AlwaysStatus(HttpStatusCode.Forbidden));

            Assert.ThrowsAny<Exception>(() => storage.ReadManifest(AzureConfig("blob=db/app.bak")));
        }

        [Fact]
        public void Azure_GetFileNames_AreTheBlobsTheWriterUsed_DateTokensExpanded()
        {
            AzureStorage storage = new AzureStorage(new FakeAzure());
            var cfg = AzureConfig("blob=db/app_{yyyy}.bak");

            storage.GetBackupWriter(cfg);

            Assert.Equal("app_" + DateTime.Now.Year + ".bak", storage.GetFileNames(cfg)[0]);
        }

        [Fact]
        public void Azure_Prune_RemovesTheManifestWithItsBackup_AndCountsABackupOnce()
        {
            FakeAzure fake = new FakeAzure();
            DateTime old = DateTime.UtcNow.AddDays(-40);
            fake.Add("/devstoreaccount1/cont/live/old.bak", "data", old);
            fake.Add("/devstoreaccount1/cont/live/old.bak.manifest.json", "{}", old);
            fake.Add("/devstoreaccount1/cont/live/new.bak", "data", DateTime.UtcNow);
            fake.Add("/devstoreaccount1/cont/live/new.bak.manifest.json", "{}", DateTime.UtcNow);

            PruneResult result = new AzureStorage(fake).PruneOldFiles(Config(AzureBase.Concat(new[] { "prefix=live/", "olderThanDays=30" }).ToArray()));

            Assert.Equal(1, result.DeletedFiles);
            Assert.False(fake.Objects.ContainsKey("/devstoreaccount1/cont/live/old.bak"));
            Assert.False(fake.Objects.ContainsKey("/devstoreaccount1/cont/live/old.bak.manifest.json"));
            Assert.True(fake.Objects.ContainsKey("/devstoreaccount1/cont/live/new.bak"));
            Assert.True(fake.Objects.ContainsKey("/devstoreaccount1/cont/live/new.bak.manifest.json"));
        }

        private sealed class AlwaysStatus : HttpMessageHandler
        {
            private readonly HttpStatusCode mCode;
            public AlwaysStatus(HttpStatusCode code) { mCode = code; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(mCode) { Content = new StringContent("denied") });
            }
        }
    }
}
