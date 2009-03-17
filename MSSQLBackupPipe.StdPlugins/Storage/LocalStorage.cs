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
        private List<bool> mDeleteOnAbort = new List<bool>();
        private List<FileInfo> mFileInfosToDeleteOnAbort = new List<FileInfo>();

        #region IBackupStorage Members

        public string GetName()
        {
            return "local";
        }

        public int GetNumberOfDevices(string config)
        {
            Dictionary<string, List<string>> parsedConfig = ConfigUtil.ParseArrayConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("local: The path property is required.");
            }


            return parsedConfig["path"].Count;
        }


        public Stream[] GetBackupWriter(string config)
        {

            mDeleteOnAbort.Clear();
            mFileInfosToDeleteOnAbort.Clear();

            Dictionary<string, List<string>> parsedConfig = ConfigUtil.ParseArrayConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("local: The path property is required.");
            }

            List<string> paths = parsedConfig["path"];
            List<FileInfo> fileInfos = paths.ConvertAll<FileInfo>(delegate(string path)
            {
                return new FileInfo(path);
            });

            parsedConfig.Remove("path");

            // initialize to false:
            mDeleteOnAbort = new List<bool>(new bool[fileInfos.Count]);
            mFileInfosToDeleteOnAbort = fileInfos;

            for (int i = 0; i < mDeleteOnAbort.Count; i++)
            {
                mDeleteOnAbort[i] = !fileInfos[i].Exists;
            }


            foreach (string key in parsedConfig.Keys)
            {
                throw new ArgumentException(string.Format("local: Unknown parameter: {0}", key));
            }

            Console.WriteLine(string.Format("local:"));
            foreach (FileInfo fi in fileInfos)
            {
                Console.WriteLine(string.Format("\tpath={0}", fi.FullName));
            }

            List<Stream> results = new List<Stream>(fileInfos.Count);
            foreach (FileInfo fi in fileInfos)
            {
                results.Add(fi.Open(FileMode.Create));
            }

            return results.ToArray();
        }

        public Stream[] GetRestoreReader(string config)
        {

            mDeleteOnAbort.Clear();
            mFileInfosToDeleteOnAbort.Clear();


            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            if (!parsedConfig.ContainsKey("path"))
            {
                throw new ArgumentException("local: The path property is required.");
            }

            FileInfo fileInfo = new FileInfo(parsedConfig["path"]);
            parsedConfig.Remove("path");



            foreach (string key in parsedConfig.Keys)
            {
                throw new ArgumentException(string.Format("local: Unknown parameter: {0}", key));
            }

            Console.WriteLine(string.Format("local: path={0}", fileInfo.FullName));

            return new Stream[] { fileInfo.Open(FileMode.Open) };
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

            for (int i = 0; i < mFileInfosToDeleteOnAbort.Count; i++)
            {
                FileInfo fi = mFileInfosToDeleteOnAbort[i];
                bool deleteOnAbort = mDeleteOnAbort[i];
                if (deleteOnAbort && fi != null)
                {
                    fi.Delete();
                }
            }
            mFileInfosToDeleteOnAbort = null;

        }

        #endregion
    }
}
