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
            if ((parsedConfig.ContainsKey("database") && parsedConfig.ContainsKey("sql"))
                || (!parsedConfig.ContainsKey("database") && !parsedConfig.ContainsKey("sql")))
            {
                throw new ArgumentException("Please provide either the database property or sql property.");
            }

            string sql = null;

            parsedConfig.TryGetValue("sql", out sql);

            if (parsedConfig.ContainsKey("database"))
            {
                sql = "BACKUP DATABASE [" + parsedConfig["database"] + "] TO VIRTUAL_DEVICE='{0}';";
            }

            return string.Format(sql, deviceName);
        }

        public string GetRestoreSqlStatement(string config, string deviceName)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config);
            if ((parsedConfig.ContainsKey("database") && parsedConfig.ContainsKey("sql"))
                || (!parsedConfig.ContainsKey("database") && !parsedConfig.ContainsKey("sql")))
            {
                throw new ArgumentException("Please provide either the database property or sql property.");
            }

            string sql;

            parsedConfig.TryGetValue("sql", out sql);

            if (parsedConfig.ContainsKey("database"))
            {
                sql = "RESTORE DATABASE [" + parsedConfig["database"] + "] FROM VIRTUAL_DEVICE='{0}';";
            }

            return string.Format(sql, deviceName);
        }

        public string GetConfigHelp()
        {
            return @"db Usage:
    db(database=<dbname>)
<dbname> should be the database name without any brackets.
or
    db(sql=<sql>)
<sql> should be the SQL command to backup or restore where {0} will be replaced by the dynamically generated device name.  By default, the backup command is:
    BACKUP DATABASE [<dbname>] TO VIRTUAL_DEVICE='{0}';
And the restore command is:
    RESTORE DATABASE [<dbname>] FROM VIRTUAL_DEVICE='{0}';
These gives the ability to add options to the BACKUP or RESTORE command, like
'WITH MOVE'.
msbp.exe has an alias for the db plugin.  A database name in brackets, like [model] is converted to db(database=model).";
}

        #endregion
    }
}
