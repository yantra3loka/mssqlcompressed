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

namespace MSSQLBackupPipe.StdPlugins.Database
{
    public class DatabaseSource : IBackupDatabase
    {

        #region IBackupDatabase Members

        public string GetName()
        {
            return "db";
        }

        public string GetBackupSqlStatement(string config, string deviceName)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            if (!parsedConfig.ContainsKey("database"))
            {
                throw new ArgumentException("Please the database property.");
            }

            string sql = "BACKUP DATABASE [" + parsedConfig["database"] + "] TO VIRTUAL_DEVICE='{0}';";

            return string.Format(sql, deviceName);
        }

        public string GetInstanceName(string config)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);

            string instanceName = null;

            if (parsedConfig.ContainsKey("instance"))
            {
                instanceName = parsedConfig["instance"];
            }

            if (instanceName != null)
            {
                instanceName = instanceName.Trim();
            }

            return instanceName;
        }


        public string GetRestoreSqlStatement(string config, string deviceName)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            if (parsedConfig.ContainsKey("database"))
            {
                throw new ArgumentException("Please the database property.");
            }

            string sql = "RESTORE DATABASE [" + parsedConfig["database"] + "] FROM VIRTUAL_DEVICE='{0}';";

            return string.Format(sql, deviceName);
        }

        public string GetConfigHelp()
        {
            return @"db Usage:
    db(database=<dbname>;instance=<instancename>)
<dbname> should be the database name without any brackets.  
<instancename> should only be the name of the instance after the slash.  If you
want to connect to localhost\sqlexpress, then enter instance=sqlexpress above.
If no instancename parameter is given, then it will connect to the default 
instance.

This plugin can only connect to SQL Server locally.

msbp.exe has an alias for the db plugin.  A database name in brackets, like [model] is converted to db(database=model).";
}

        #endregion
    }
}
