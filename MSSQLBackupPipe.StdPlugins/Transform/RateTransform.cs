using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSSQLBackupPipe.StdPlugins.Transform
{
    public class RateTransform : IBackupTransformer
    {

        #region IBackupTransformer Members

        public Stream GetBackupWriter(string config, Stream writeToStream)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            double rateMb;

            if (!parsedConfig.ContainsKey("ratemb"))
            {
                throw new ArgumentException("The ratemb parameter is missing.  Use the rate option like rate(ratemb=10)");
            }

            rateMb = double.Parse(parsedConfig["ratemb"]);

            return new RateLimitStream(writeToStream, rateMb);
        }

        public Stream GetRestoreReader(string config, Stream readFromStream)
        {
            return GetBackupWriter(config, readFromStream);
        }

        #endregion

        #region IBackupPlugin Members

        public string GetName()
        {
            return "rate";
        }

        public string GetConfigHelp()
        {
            return @"rate Usage:
You can slow down the pipeline to ensure the server is not overloaded.  Enter a rate in MB like:
    rate(rateMB=10.0)";
        }

        #endregion


        private class RateLimitStream : Stream
        {
            private Stream mStream;
            private double mRateMB;
            private DateTime mNextStartTimeUtc;
            private bool mDisposed;

            public RateLimitStream(Stream s, double rateMB)
            {
                mStream = s;
                mRateMB = rateMB;
                mNextStartTimeUtc = DateTime.UtcNow;
            }

            public override bool CanRead
            {
                get { return mStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return mStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return mStream.CanWrite; }
            }

            public override void Flush()
            {
                mStream.Flush();
            }

            public override long Length
            {
                get { return mStream.Length; }
            }

            public override long Position
            {
                get
                {
                    return mStream.Position;
                }
                set
                {
                    mStream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                while (mNextStartTimeUtc > DateTime.UtcNow)
                {
                    System.Threading.Thread.Sleep(mNextStartTimeUtc - DateTime.UtcNow);
                }

                mNextStartTimeUtc = DateTime.UtcNow.AddSeconds(((double)count) / mRateMB / (1024 * 1024));

                return mStream.Read(buffer, offset, count);

            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return mStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                mStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (mNextStartTimeUtc > DateTime.UtcNow)
                {
                    System.Threading.Thread.Sleep(mNextStartTimeUtc - DateTime.UtcNow);
                }

                mStream.Write(buffer, offset, count);

                mNextStartTimeUtc = DateTime.UtcNow.AddSeconds(((double)count) / mRateMB / (1024 * 1024));
            }

            protected override void Dispose(bool disposing)
            {
                if (!mDisposed)
                {
                    if (disposing)
                    {
                        mStream.Dispose();
                        // dispose of managed resources

                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);

            }
        }
    }
}
