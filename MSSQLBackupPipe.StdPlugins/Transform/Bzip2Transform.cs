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

using ICSharpCode.SharpZipLib.BZip2;


namespace MSSQLBackupPipe.StdPlugins
{
    public class Bzip2Transform : IBackupTransformer
    {

        #region IBackupTransformer Members

        public  Stream GetBackupWriter(string config, Stream writeToStream)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            int level = 1;
            string sLevel;
            if (parsedConfig.TryGetValue("level", out sLevel))
            {
                if (!int.TryParse(sLevel, out level))
                {
                    throw new ArgumentException(string.Format("bzip2: Unable to parse the integer: {0}", sLevel));
                }
            }

            if (level < 1 || level > 9)
            {
                throw new ArgumentException(string.Format("bzip2: Level must be between 1 and 9: {0}", level));
            }

            Console.WriteLine(string.Format("bzip2: level = {0}", level));

            return new BZip2OutputStream(writeToStream, level);
        }

        public string GetName()
        {
            return "bzip2";
        }

        public Stream GetRestoreReader(string config, Stream readFromStream)
        {
            Console.WriteLine(string.Format("bzip2"));

            return new BZip2InputStream(readFromStream);
        }


        public string GetConfigHelp()
        {
            return @"bzip2 Usage:
bzip2 will compress (or uncompress) the data.
By default bzip2 compresses with level=1.  You use a level from 1 to 9, for 
example:
    bzip(level=5)
Level is ignored when restoring a database since the data is being uncompressed.";
        }

        #endregion
    }
}
