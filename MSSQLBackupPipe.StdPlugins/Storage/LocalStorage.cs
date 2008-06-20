/*
	Copyright 2008 Clay Lenhart <clay@lenharts.net>


	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
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
using System.Text;
using System.IO;

namespace MSSQLBackupPipe.StdPlugins.Storage
{
    public class LocalStorage : IBackupStorage
    {
        private bool mDeleteOnAbort;
        private FileInfo mFileInfoToDeleteOnAbort;

        #region IBackupStorage Members

        public string GetName()
        {
            return "local";
        }

        public Stream GetBackupWriter(string config)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("local: The path property is required.");
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
                throw new ArgumentException("local: The path property is required.");
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
