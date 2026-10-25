// Round-trip tests for the SqlSqueeze pipeline plugins.
// No SQL Server needed: these exercise the compression/storage streams only.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SqlSqueeze.Plugins;
using SqlSqueeze.Plugins.Transform;
using Xunit;

namespace SqlSqueeze.Tests
{
    public class PipelineTests
    {
        private static Dictionary<string, List<string>> Cfg(params (string, string)[] pairs)
        {
            var d = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            foreach ((string k, string v) in pairs)
            {
                d[k] = new List<string> { v };
            }
            return d;
        }

        private static Dictionary<string, List<string>> Empty()
        {
            return new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        }

        private static byte[] SampleData(int size = 1 << 20)
        {
            byte[] data = new byte[size];
            new Random(42).NextBytes(data);
            Array.Clear(data, 0, data.Length / 2); // half zeros: exercises real compression
            return data;
        }

        private static byte[] RoundTrip(IBackupTransformer t, Dictionary<string, List<string>> wcfg, Dictionary<string, List<string>> rcfg, byte[] data)
        {
            byte[] blob;
            using (MemoryStream ms = new MemoryStream())
            {
                using (Stream w = t.GetBackupWriter(wcfg, ms))
                {
                    w.Write(data, 0, data.Length);
                }
                blob = ms.ToArray();
            }
            using (MemoryStream ms = new MemoryStream(blob, writable: false))
            using (Stream r = t.GetRestoreReader(rcfg, ms))
            using (MemoryStream outms = new MemoryStream())
            {
                r.CopyTo(outms);
                return outms.ToArray();
            }
        }

        [Fact]
        public void Gzip_RoundTrip()
        {
            byte[] data = SampleData();
            Assert.Equal(data, RoundTrip(new GzipTransform(), Cfg(("level", "5")), Empty(), data));
        }

        [Fact]
        public void Bzip2_RoundTrip()
        {
            byte[] data = SampleData();
            Assert.Equal(data, RoundTrip(new Bzip2Transform(), Cfg(("level", "5")), Empty(), data));
        }

        [Fact]
        public void Zip64_RoundTrip_NamedEntry()
        {
            byte[] data = SampleData();
            Assert.Equal(data, RoundTrip(new Zip64Transform(),
                Cfg(("level", "5"), ("filename", "model.bak")),
                Cfg(("filename", "model.bak")), data));
        }

        [Fact]
        public void Zip64_RoundTrip_FirstEntry()
        {
            byte[] data = SampleData();
            Assert.Equal(data, RoundTrip(new Zip64Transform(), Cfg(("level", "5")), Empty(), data));
        }

        [Fact]
        public void Rate_PassesThrough()
        {
            byte[] data = SampleData();
            Assert.Equal(data, RoundTrip(new RateTransform(),
                Cfg(("ratemb", "500")), Cfg(("ratemb", "500")), data));
        }

        [Fact]
        public void LocalStorage_RoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "sqlsqueeze-test-" + Guid.NewGuid() + ".bin");
            try
            {
                var store = new SqlSqueeze.Plugins.Storage.LocalStorage();
                Dictionary<string, List<string>> cfg = Cfg(("path", path));
                byte[] data = SampleData(65536);
                using (Stream ws = store.GetBackupWriter(cfg)[0])
                {
                    ws.Write(data, 0, data.Length);
                }
                long estimated;
                using (Stream rs = store.GetRestoreReader(cfg, out estimated)[0])
                using (MemoryStream ms = new MemoryStream())
                {
                    rs.CopyTo(ms);
                    Assert.Equal(data, ms.ToArray());
                }
                Assert.Equal(data.Length, estimated);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
