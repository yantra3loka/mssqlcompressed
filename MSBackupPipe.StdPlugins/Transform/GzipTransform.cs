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


namespace MSBackupPipe.StdPlugins
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Gzip")]
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
                if (!int.TryParse(sLevel, out level))
                {
                    throw new ArgumentException(string.Format("gzip: Unable to parse the integer: {0}", sLevel));
                }
            }

            if (level < 1 || level > 9)
            {
                throw new ArgumentException(string.Format("gzip: Level must be between 1 and 9: {0}", level));
            }


            parsedConfig.Remove("level");



            foreach (string key in parsedConfig.Keys)
            {
                throw new ArgumentException(string.Format("gzip: Unknown parameter: {0}", key));
            }

            Console.WriteLine(string.Format("gzip: level = {0}", level));

            return new GZipOutputStream(writeToStream, level);
        }

        public string Name
        {
            get { return "gzip"; }
        }

        public Stream GetRestoreReader(string config, Stream readFromStream)
        {
            Console.WriteLine(string.Format("gzip"));

            return new GZipInputStream(readFromStream);
        }

        public string CommandLineHelp
        {
            get
            {
                return @"gzip Usage:
gzip will compress (or uncompress) the data.
By default gzip compresses with level=9.  You use a level from 1 to 9, for 
example:
    gzip(level=5)
Level is ignored when restoring a database since the data is being uncompressed.";
            }
        }

        #endregion
    }
}
