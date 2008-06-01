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

        Stream IBackupTransformer.GetBackupWriter(string config, Stream writeToStream)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            int level = 9;
            string sLevel;
            if (parsedConfig.TryGetValue("level", out sLevel))
            {
                int.TryParse(sLevel, out level);
            }

            Console.WriteLine(string.Format("BzipTransform: level = {0}", level));

            return new GZipOutputStream(writeToStream, level);
        }

        string IBackupTransformer.GetName()
        {
            return "gzip";
        }

        Stream IBackupTransformer.GetRestoreReader(string config, Stream readFromStream)
        {
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
