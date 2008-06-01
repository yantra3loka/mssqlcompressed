using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSSQLBackupPipe.StdPlugins
{
    public interface IBackupTransformer
    {
        string GetName();
        Stream GetBackupWriter(string config, Stream writeToStream);
        Stream GetRestoreReader(string config, Stream readFromStream);
        string GetConfigHelp();
    }
}
