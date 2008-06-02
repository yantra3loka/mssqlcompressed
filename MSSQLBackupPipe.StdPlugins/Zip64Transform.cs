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

using ICSharpCode.SharpZipLib.Zip;

namespace MSSQLBackupPipe.StdPlugins
{
    public class Zip64Transform : IBackupTransformer
    {
        #region IBackupTransformer Members

        Stream IBackupTransformer.GetBackupWriter(string config, Stream writeToStream)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            string filename = "database.bak";
            int level = 7;

            string sLevel;
            if (parsedConfig.TryGetValue("level", out sLevel))
            {
                int.TryParse(sLevel, out level);
            }

            parsedConfig.TryGetValue("filename", out filename);

            Console.WriteLine(string.Format("Zip64Transform: level = {0}, filename={1}", level, filename));



            return new OneFileZipOutputStream(filename, level, writeToStream);
        }

        string IBackupTransformer.GetName()
        {
            return "zip64";
        }

        Stream IBackupTransformer.GetRestoreReader(string config, Stream readFromStream)
        {
            Console.WriteLine(string.Format("Zip64Transform"));

            return new FirstFileZipInputStream(readFromStream);
        }

        public string GetConfigHelp()
        {
            //TODO: GetConfigHelp
            return @"";
        }

        #endregion






        private class OneFileZipOutputStream : ZipOutputStream
        {
            private bool mDisposed;
            public OneFileZipOutputStream(string internalFilename, int compressionLevel, Stream writeToStream)
                : base(writeToStream)
            {


                ZipEntry entry = new ZipEntry(internalFilename);


                base.IsStreamOwner = true;
                base.PutNextEntry(entry);
                base.SetLevel(compressionLevel);

                mDisposed = false;
            }


            protected override void Dispose(bool disposing)
            {
                if (!mDisposed)
                {
                    if (disposing)
                    {
                        base.Finish();
                        base.Close();
                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);

            }



        }


        private class FirstFileZipInputStream : ZipInputStream
        {
            private bool mDisposed;
            public FirstFileZipInputStream(Stream readFromStream)
                : base(readFromStream)
            {

                base.IsStreamOwner = true;
                ZipEntry entry = base.GetNextEntry();

                if (entry == null)
                {
                    throw new NullReferenceException("The zip file is empty.");
                }

                


                mDisposed = false;
            }


            protected override void Dispose(bool disposing)
            {
                if (!mDisposed)
                {
                    if (disposing)
                    {
                        base.Close();
                        // dispose of managed resources

                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);

            }



        }
    }




    
}
