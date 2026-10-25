// AES transform tests. No SQL Server needed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SqlSqueeze.Plugins;
using Xunit;

namespace SqlSqueeze.Tests
{
    public class AesTests
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

        private static byte[] SampleData()
        {
            byte[] data = new byte[65536];
            new Random(11).NextBytes(data);
            return data;
        }

        private static byte[] Encrypt(byte[] data, Dictionary<string, List<string>> cfg)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Stream w = new AesTransform().GetBackupWriter(cfg, ms))
                {
                    w.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }

        private static byte[] Decrypt(byte[] blob, Dictionary<string, List<string>> cfg)
        {
            using (MemoryStream ms = new MemoryStream(blob, writable: false))
            using (Stream r = new AesTransform().GetRestoreReader(cfg, ms))
            using (MemoryStream outms = new MemoryStream())
            {
                r.CopyTo(outms);
                return outms.ToArray();
            }
        }

        [Fact]
        public void RoundTrip_Password()
        {
            byte[] data = SampleData();
            byte[] blob = Encrypt(data, Cfg(("password", "s3cret"), ("iterations", "1000")));
            Assert.Equal(data, Decrypt(blob, Cfg(("password", "s3cret"), ("iterations", "1000"))));
        }

        [Fact]
        public void RoundTrip_PasswordEnv()
        {
            const string var = "SQLSQUEEZE_TEST_KEY";
            Environment.SetEnvironmentVariable(var, "env-s3cret");
            try
            {
                byte[] data = SampleData();
                byte[] blob = Encrypt(data, Cfg(("passwordenv", var), ("iterations", "1000")));
                Assert.Equal(data, Decrypt(blob, Cfg(("passwordenv", var), ("iterations", "1000"))));
            }
            finally
            {
                Environment.SetEnvironmentVariable(var, null);
            }
        }

        [Fact]
        public void CiphertextsDiffer_PerBackupSalt()
        {
            byte[] data = SampleData();
            var cfg = Cfg(("password", "s3cret"), ("iterations", "1000"));
            Assert.NotEqual(Encrypt(data, cfg), Encrypt(data, cfg));
        }

        [Fact]
        public void GoldenVector_Decrypts()
        {
            // Blob encrypted with the original Rfc2898DeriveBytes ctor code.
            // Guards the KDF switch: old backups must keep decrypting.
            var asm = typeof(AesTests).Assembly;
            using (Stream res = asm.GetManifestResourceStream("SqlSqueeze.Tests.Data.aes-golden.bin"))
            {
                Assert.NotNull(res);
                using (MemoryStream ms = new MemoryStream())
                {
                    res.CopyTo(ms);
                    byte[] plain = Decrypt(ms.ToArray(), Cfg(("password", "golden-key-2026"), ("iterations", "1000")));
                    Assert.Equal("SqlSqueeze AES golden file. 0123456789 ABCDEFGHIJ",
                        System.Text.Encoding.UTF8.GetString(plain));
                }
            }
        }

        [Fact]
        public void WrongPassword_Throws()
        {
            byte[] data = SampleData();
            var good = Cfg(("password", "right"), ("iterations", "1000"));
            var bad = Cfg(("password", "wrong"), ("iterations", "1000"));
            byte[] blob = Encrypt(data, good);
            Assert.Throws<CryptographicException>(() => Decrypt(blob, bad));
        }

        [Fact]
        public void MissingPassword_Throws()
        {
            var empty = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            Assert.Throws<ArgumentException>(() =>
                new AesTransform().GetBackupWriter(empty, new MemoryStream()));
        }

        [Fact]
        public void TruncatedHeader_Throws()
        {
            var cfg = Cfg(("password", "s3cret"), ("iterations", "1000"));
            Assert.Throws<EndOfStreamException>(() => Decrypt(new byte[10], cfg));
        }
    }
}
