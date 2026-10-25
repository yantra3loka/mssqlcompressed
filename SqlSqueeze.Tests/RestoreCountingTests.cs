// Regression test: restore-side byte counting must equal the file size.
using System;
using System.Collections.Generic;
using System.IO;
using SqlSqueeze.Core;
using SqlSqueeze.Plugins;
using Xunit;

namespace SqlSqueeze.Tests
{
    public class RestoreCountingTests
    {
        private class NullNotifier : IUpdateNotification
        {
            public void OnConnecting(string message) { }
            public void OnStart() { }
            public TimeSpan OnStatusUpdate(float percentComplete) { return TimeSpan.Zero; }
        }

        private static Dictionary<string, List<string>> Empty()
        {
            return new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        }

        private static Dictionary<string, List<string>> Cfg(params (string, string)[] pairs)
        {
            var d = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            foreach ((string k, string v) in pairs)
            {
                d[k] = new List<string> { v };
            }
            return d;
        }

        [Fact]
        public void RestorePipeline_CountsAllBytes()
        {
            byte[] data = new byte[1 << 20];
            new Random(3).NextBytes(data);

            // Build a gzip file the way backup does.
            byte[] blob;
            using (MemoryStream ms = new MemoryStream())
            {
                using (Stream w = new GzipTransform().GetBackupWriter(Empty(), ms))
                {
                    w.Write(data, 0, data.Length);
                }
                blob = ms.ToArray();
            }

            // Restore side: file -> tracking -> gzip, read to end.
            var notification = new InternalStreamNotification(new NullNotifier());
            long total;
            using (MemoryStream file = new MemoryStream(blob, writable: false))
            using (TrackingStream tracking = new TrackingStream(file, notification))
            using (Stream gzip = new GzipTransform().GetRestoreReader(Empty(), tracking))
            using (MemoryStream outms = new MemoryStream())
            {
                gzip.CopyTo(outms);
                total = notification.TotalBytesProcessed;
                Assert.Equal(data, outms.ToArray());
            }
            Assert.Equal(blob.Length, total);
        }

        [Fact]
        public void RestorePipeline_ZstdAes_CountsAllBytes()
        {
            // Live shape: file -> tracking -> aes -> zstd, drained through
            // CommandBuffer.ReadFromStream exactly like DeviceThread does.
            byte[] data = new byte[1 << 20];
            new Random(9).NextBytes(data);
            var aesCfg = Cfg(("password", "counting"), ("iterations", "1000"));

            byte[] blob;
            using (MemoryStream ms = new MemoryStream())
            {
                using (Stream aes = new AesTransform().GetBackupWriter(aesCfg, ms))
                using (Stream zstd = new ZstdTransform().GetBackupWriter(Cfg(("level", "3")), aes))
                {
                    zstd.Write(data, 0, data.Length);
                }
                blob = ms.ToArray();
            }

            var notification = new InternalStreamNotification(new NullNotifier());
            long total;
            using (MemoryStream file = new MemoryStream(blob, writable: false))
            using (TrackingStream tracking = new TrackingStream(file, notification))
            using (Stream aes = new AesTransform().GetRestoreReader(aesCfg, tracking))
            using (Stream zstd = new ZstdTransform().GetRestoreReader(Empty(), aes))
            using (MemoryStream outms = new MemoryStream())
            {
                zstd.CopyTo(outms);
                Assert.Equal(data, outms.ToArray());
                total = notification.TotalBytesProcessed;
            }
            Assert.Equal(blob.Length, total);
        }
    }
}
