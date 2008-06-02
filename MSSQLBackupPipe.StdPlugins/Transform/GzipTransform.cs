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


using ICSharpCode.SharpZipLib.GZip;


namespace MSSQLBackupPipe.StdPlugins
{
    public class GzipTransform : IBackupTransformer
    {

        #region IBackupTransformer Members

        public Stream GetBackupWriter(string config, Stream writeToStream)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            int level = 9;
            string sLevel;
            if (parsedConfig.TryGetValue("level", out sLevel))
            {
                int.TryParse(sLevel, out level);
            }

            Console.WriteLine(string.Format("GzipTransform: level = {0}", level));

            return new GZipOutputStream(writeToStream, level);
        }

        public string GetName()
        {
            return "gzip";
        }

        public Stream GetRestoreReader(string config, Stream readFromStream)
        {
            Console.WriteLine(string.Format("GzipTransform"));

            return new GZipInputStream(readFromStream);
        }

        public string GetConfigHelp()
        {
            //TODO: GetConfigHelp
            return @"";
        }

        #endregion
    }
}
