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
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
