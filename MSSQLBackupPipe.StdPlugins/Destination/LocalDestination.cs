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
            return @"local Usage:
This is a plugin to store or read a backup file.
To reference a file, enter:
local(path=<file>)

msbp.exe has an alias for the local plugin.  If it begins with file://, it is
converted to the 'local' plugin equivalent.  file:///c:\model.bak is converted
to local(path=c:\model.bak).
";
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
