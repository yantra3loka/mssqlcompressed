using System;
using System.Collections.Generic;
using System.Text;

namespace MSSQLBackupPipe.StdPlugins
{
    public interface IBackupPlugin
    {
        string GetName();
    }
}
