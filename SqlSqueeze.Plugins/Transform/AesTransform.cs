/*
	Copyright 2026 Krishnan Venkiteswaran

	This file is part of SqlSqueeze (formerly MSSQL Compressed Backup).

    SqlSqueeze is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SqlSqueeze.Plugins
{
    /// <summary>
    /// AES-256-CBC encryption with a per-backup random salt+IV and a
    /// PBKDF2-SHA256 key. Compress BEFORE encrypting:
    ///   sqlsqueeze backup [db] gzip aes(passwordEnv=BACKUP_KEY) local(path=x)
    /// Prefer passwordEnv over password: command lines are visible in
    /// process listings and job history.
    /// </summary>
    public class AesTransform : IBackupTransformer
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int DefaultIterations = 100000;

        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        static AesTransform()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("password", new ParameterInfo(false, false));
            mBackupParamSchema.Add("passwordenv", new ParameterInfo(false, false));
            mBackupParamSchema.Add("iterations", new ParameterInfo(false, false));

            mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mRestoreParamSchema.Add("password", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("passwordenv", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("iterations", new ParameterInfo(false, false));
        }

        public string Name
        {
            get { return "aes"; }
        }

        public string CommandLineHelp
        {
            get
            {
                return @"aes Usage:
aes encrypts (or decrypts) with AES-256-CBC. Always compress BEFORE encrypting:
    sqlsqueeze backup [mydb] gzip aes(passwordEnv=SQLSQUEEZE_KEY) local(path=c:\b.bak.gz.aes)
    sqlsqueeze restore local(path=c:\b.bak.gz.aes) aes(passwordEnv=SQLSQUEEZE_KEY) gzip [mydb]
Takes password=... (visible in process lists, avoid) or passwordEnv=VAR
(reads the password from an environment variable, recommended), plus
optional iterations=N (PBKDF2 rounds, default 100000; must match on restore).";
            }
        }

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);
            string password = ResolvePassword(config, true);
            int iterations = ResolveIterations(config);

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(iv);

            // Header first, in the clear: salt + IV.
            writeToStream.Write(salt, 0, salt.Length);
            writeToStream.Write(iv, 0, iv.Length);

            Console.WriteLine(string.Format("aes: encrypting, iterations = {0}", iterations));
            return new CryptoStream(writeToStream, CreateTransform(password, salt, iterations, iv, true), CryptoStreamMode.Write);
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);
            string password = ResolvePassword(config, false);
            int iterations = ResolveIterations(config);

            byte[] salt = ReadExact(readFromStream, SaltSize);
            byte[] iv = ReadExact(readFromStream, IvSize);

            Console.WriteLine("aes: decrypting");
            return new CryptoStream(readFromStream, CreateTransform(password, salt, iterations, iv, false), CryptoStreamMode.Read);
        }

        private static ICryptoTransform CreateTransform(string password, byte[] salt, int iterations, byte[] iv, bool encrypt)
        {
            byte[] key;
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                key = kdf.GetBytes(32);
            }
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
                CryptographicOperations.ZeroMemory(key);
                return transform;
            }
        }

        private static string ResolvePassword(Dictionary<string, List<string>> config, bool isBackup)
        {
            string action = isBackup ? "encrypting" : "decrypting";
            if (config.TryGetValue("passwordenv", out List<string> envNames))
            {
                string value = Environment.GetEnvironmentVariable(envNames[0]);
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(string.Format("aes: environment variable is empty or missing, cannot {0}: {1}", action, envNames[0]));
                }
                return value;
            }
            if (config.TryGetValue("password", out List<string> passwords))
            {
                if (string.IsNullOrEmpty(passwords[0]))
                {
                    throw new ArgumentException(string.Format("aes: empty password, cannot {0}.", action));
                }
                return passwords[0];
            }
            throw new ArgumentException(string.Format("aes: password= or passwordEnv= is required for {0}.", action));
        }

        private static int ResolveIterations(Dictionary<string, List<string>> config)
        {
            if (config.TryGetValue("iterations", out List<string> values))
            {
                if (!int.TryParse(values[0], out int iterations) || iterations < 1000)
                {
                    throw new ArgumentException(string.Format("aes: iterations must be an integer >= 1000: {0}", values[0]));
                }
                return iterations;
            }
            return DefaultIterations;
        }

        private static byte[] ReadExact(Stream source, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = source.Read(buf, read, count - read);
                if (n == 0)
                {
                    throw new EndOfStreamException("aes: backup file is truncated (missing salt/IV header).");
                }
                read += n;
            }
            return buf;
        }
    }
}
