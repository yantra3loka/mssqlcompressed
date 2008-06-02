using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSSQLBackupPipe.StdPlugins.Destination
{
    public class LocalDestination : IBackupDestination
    {
        private bool mDeleteOnAbort;
        private FileInfo mFileInfoToDeleteOnAbort;

        #region IBackupDestination Members

        public string GetName()
        {
            return "local";
        }

        public Stream GetBackupWriter(string config)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("The path property is required.");
            }

            FileInfo fileInfo = new FileInfo(parsedConfig["path"]);

            if (fileInfo.Exists)
            {
                mDeleteOnAbort = false;
            }
            else
            {
                mDeleteOnAbort = true;
                mFileInfoToDeleteOnAbort = fileInfo;
            }

            Console.WriteLine(string.Format("local: path={0}", fileInfo.FullName));

            return fileInfo.Open(FileMode.Create);
        }

        public Stream GetRestoreReader(string config)
        {
            mDeleteOnAbort = false;


            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("The path property is required.");
            }

            FileInfo fileInfo = new FileInfo(parsedConfig["path"]);
            Console.WriteLine(string.Format("local: path={0}", fileInfo.FullName));

            return fileInfo.Open(FileMode.Open);
        }

        public string GetConfigHelp()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void CleanupOnAbort()
        {
            if (mDeleteOnAbort && mFileInfoToDeleteOnAbort != null)
            {
                mFileInfoToDeleteOnAbort.Delete();
            }
        }

        #endregion
    }
}
