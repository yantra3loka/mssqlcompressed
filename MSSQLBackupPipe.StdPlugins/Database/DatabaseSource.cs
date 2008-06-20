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
using System.Data;
using System.Data.SqlClient;

namespace MSSQLBackupPipe.StdPlugins.Database
{
    public class DatabaseSource : IBackupDatabase
    {

        #region IBackupDatabase Members

        public string GetName()
        {
            return "db";
        }

        public void ConfigureBackupCommand(string config, string device, SqlCommand cmd)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config, "FILE", "FILEGROUP");
            Dictionary<string, List<string>> parsedArrayConfig = ConfigUtil.ParseArrayConfig(config);

            if (!parsedConfig.ContainsKey("database"))
            {
                throw new ArgumentException("db: Please enter the database property.");
            }

            SqlParameter param;
            param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255);
            param.Value = parsedConfig["database"];
            cmd.Parameters.Add(param);

            parsedArrayConfig.Remove("database");
            parsedConfig.Remove("database");

            // instancename is ignored in the method
            parsedArrayConfig.Remove("instancename");
            parsedConfig.Remove("instancename");

            // default values:
            BackupType backupType = BackupType.Full;


            if (parsedConfig.ContainsKey("backuptype"))
            {
                switch (parsedConfig["backuptype"])
                {
                    case "full":
                        backupType = BackupType.Full;
                        break;
                    case "differential":
                        backupType = BackupType.Differential;
                        break;
                    case "log":
                        backupType = BackupType.Log;
                        break;
                    default:
                        throw new ArgumentException(string.Format("db: Unknown backuptype: {0}", parsedConfig["backuptype"]));
                }
            }
            parsedArrayConfig.Remove("backuptype");
            parsedConfig.Remove("backuptype");

            List<string> withOptions = new List<string>();
            List<string> filegroupOptions = new List<string>();

            if (parsedConfig.ContainsKey("READ_WRITE_FILEGROUPS"))
            {
                filegroupOptions.Add("READ_WRITE_FILEGROUPS");
            }
            parsedConfig.Remove("READ_WRITE_FILEGROUPS");
            parsedArrayConfig.Remove("READ_WRITE_FILEGROUPS");

            if (parsedArrayConfig.ContainsKey("FILE"))
            {
                int i = 0;
                foreach (string file in parsedArrayConfig["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = file;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }
            parsedConfig.Remove("FILE");
            parsedArrayConfig.Remove("FILE");


            if (parsedArrayConfig.ContainsKey("FILEGROUP"))
            {
                int i = 0;
                foreach (string filegroup in parsedArrayConfig["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = filegroup;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }
            parsedConfig.Remove("FILEGROUP");
            parsedArrayConfig.Remove("FILEGROUP");



            if (parsedConfig.ContainsKey("COPY_ONLY"))
            {
                withOptions.Add("COPY_ONLY");
            }
            parsedConfig.Remove("COPY_ONLY");
            parsedArrayConfig.Remove("COPY_ONLY");

            if (parsedConfig.ContainsKey("CHECKSUM"))
            {
                withOptions.Add("CHECKSUM");
            }
            parsedConfig.Remove("CHECKSUM");
            parsedArrayConfig.Remove("CHECKSUM");

            if (parsedConfig.ContainsKey("NO_CHECKSUM"))
            {
                withOptions.Add("NO_CHECKSUM");
            }
            parsedConfig.Remove("NO_CHECKSUM");
            parsedArrayConfig.Remove("NO_CHECKSUM");

            if (parsedConfig.ContainsKey("STOP_ON_ERROR"))
            {
                withOptions.Add("STOP_ON_ERROR");
            }
            parsedConfig.Remove("STOP_ON_ERROR");
            parsedArrayConfig.Remove("STOP_ON_ERROR");

            if (parsedConfig.ContainsKey("CONTINUE_AFTER_ERROR"))
            {
                withOptions.Add("CONTINUE_AFTER_ERROR");
            }
            parsedConfig.Remove("CONTINUE_AFTER_ERROR");
            parsedArrayConfig.Remove("CONTINUE_AFTER_ERROR");


            foreach (string key in parsedConfig.Keys)
            {
                throw new ArgumentException(string.Format("db: Unknown parameter: {0}", key));
            }

            foreach (string key in parsedArrayConfig.Keys)
            {
                throw new ArgumentException(string.Format("db: Unknown parameter: {0}", key));
            }

            if (backupType == BackupType.Differential)
            {
                withOptions.Insert(0, "DIFFERENTIAL");
            }

            string filegroupClause = null;
            if (filegroupOptions.Count > 0)
            {
                for (int i = 0; i < filegroupOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        filegroupClause += ",";
                    }
                    filegroupClause += filegroupOptions[i];
                }
                filegroupClause += " ";
            }

            string withClause = null;
            if (withOptions.Count > 0)
            {
                withClause = " WITH ";
                for (int i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }


            string databaseOrLog = backupType == BackupType.Log ? "LOG" : "DATABASE";


            cmd.CommandType = CommandType.Text;
            cmd.CommandText = string.Format("BACKUP {0} @databasename {1}TO VIRTUAL_DEVICE='{2}'{3};", databaseOrLog, filegroupClause, device, withClause);
        }

        public string GetInstanceName(string config)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config, "file", "filegroup", "move");

            string instanceName = null;

            if (parsedConfig.ContainsKey("instancename"))
            {
                instanceName = parsedConfig["instancename"];
            }

            if (instanceName != null)
            {
                instanceName = instanceName.Trim();
            }

            return instanceName;
        }


        public void ConfigureRestoreCommand(string config, string device, SqlCommand cmd)
        {
            Dictionary<string, string> parsedConfig = ConfigUtil.ParseConfig(config, "FILE", "FILEGROUP", "MOVE");
            Dictionary<string, List<string>> parsedArrayConfig = ConfigUtil.ParseArrayConfig(config);


            if (!parsedConfig.ContainsKey("database"))
            {
                throw new ArgumentException("db: Please enter the database property.");
            }

            SqlParameter param;
            param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255);
            param.Value = parsedConfig["database"];
            cmd.Parameters.Add(param);

            parsedArrayConfig.Remove("database");
            parsedConfig.Remove("database");

            // instancename is ignored in the method
            parsedArrayConfig.Remove("instancename");
            parsedConfig.Remove("instancename");


            // default values:
            RestoreType restoreType = RestoreType.Database;

            if (parsedConfig.ContainsKey("restoretype"))
            {
                switch (parsedConfig["restoretype"])
                {
                    case "database":
                        restoreType = RestoreType.Database;
                        break;
                    case "log":
                        restoreType = RestoreType.Log;
                        break;
                    default:
                        throw new ArgumentException(string.Format("db: Unknown restoreType: {0}", parsedConfig["restoretype"]));
                }
            }


            List<string> withOptions = new List<string>();
            List<string> filegroupOptions = new List<string>();

            if (parsedConfig.ContainsKey("CHECKSUM"))
            {
                withOptions.Add("CHECKSUM");
            }
            parsedConfig.Remove("CHECKSUM");
            parsedArrayConfig.Remove("CHECKSUM");


            if (parsedConfig.ContainsKey("NO_CHECKSUM"))
            {
                withOptions.Add("NO_CHECKSUM");
            }
            parsedConfig.Remove("NO_CHECKSUM");
            parsedArrayConfig.Remove("NO_CHECKSUM");


            if (parsedConfig.ContainsKey("STOP_ON_ERROR"))
            {
                withOptions.Add("STOP_ON_ERROR");
            }
            parsedConfig.Remove("STOP_ON_ERROR");
            parsedArrayConfig.Remove("STOP_ON_ERROR");

            if (parsedConfig.ContainsKey("CONTINUE_AFTER_ERROR"))
            {
                withOptions.Add("CONTINUE_AFTER_ERROR");
            }
            parsedConfig.Remove("CONTINUE_AFTER_ERROR");
            parsedArrayConfig.Remove("CONTINUE_AFTER_ERROR");

            if (parsedConfig.ContainsKey("KEEP_REPLICATION"))
            {
                withOptions.Add("KEEP_REPLICATION");
            }
            parsedConfig.Remove("KEEP_REPLICATION");
            parsedArrayConfig.Remove("KEEP_REPLICATION");


            if (parsedConfig.ContainsKey("ENABLE_BROKER"))
            {
                withOptions.Add("ENABLE_BROKER");
            }
            parsedConfig.Remove("ENABLE_BROKER");
            parsedArrayConfig.Remove("ENABLE_BROKER");


            if (parsedConfig.ContainsKey("ERROR_BROKER_CONVERSATIONS"))
            {
                withOptions.Add("ERROR_BROKER_CONVERSATIONS");
            }
            parsedConfig.Remove("ERROR_BROKER_CONVERSATIONS");
            parsedArrayConfig.Remove("ERROR_BROKER_CONVERSATIONS");


            if (parsedConfig.ContainsKey("NEW_BROKER"))
            {
                withOptions.Add("NEW_BROKER");
            }
            parsedConfig.Remove("NEW_BROKER");
            parsedArrayConfig.Remove("NEW_BROKER");


            if (parsedConfig.ContainsKey("RECOVERY"))
            {
                withOptions.Add("RECOVERY");
            }
            parsedConfig.Remove("RECOVERY");
            parsedArrayConfig.Remove("RECOVERY");


            if (parsedConfig.ContainsKey("NORECOVERY"))
            {
                withOptions.Add("NORECOVERY");
            }
            parsedConfig.Remove("NORECOVERY");
            parsedArrayConfig.Remove("NORECOVERY");


            if (parsedConfig.ContainsKey("STANDBY"))
            {
                string standbyFile = parsedConfig["STANDBY"].Trim();
                if (standbyFile.StartsWith("'"))
                {
                    standbyFile = standbyFile.Substring(1);
                }
                if (standbyFile.EndsWith("'"))
                {
                    standbyFile = standbyFile.Substring(0, standbyFile.Length - 1);
                }
                if (standbyFile.Contains("'"))
                {
                    throw new ArgumentException("db: The standby filename cannot have a singe quote (') in the path.");
                }
                withOptions.Add(string.Format("STANDBY='{0}'", standbyFile));
            }
            parsedConfig.Remove("STANDBY");
            parsedArrayConfig.Remove("STANDBY");


            if (parsedConfig.ContainsKey("REPLACE"))
            {
                withOptions.Add("REPLACE");
            }
            parsedConfig.Remove("REPLACE");
            parsedArrayConfig.Remove("REPLACE");


            if (parsedConfig.ContainsKey("RESTART"))
            {
                withOptions.Add("RESTART");
            }
            parsedConfig.Remove("RESTART");
            parsedArrayConfig.Remove("RESTART");


            if (parsedConfig.ContainsKey("RESTRICTED_USER"))
            {
                withOptions.Add("RESTRICTED_USER");
            }
            parsedConfig.Remove("RESTRICTED_USER");
            parsedArrayConfig.Remove("RESTRICTED_USER");


            if (parsedConfig.ContainsKey("STOPAT"))
            {
                DateTime stopAtDateTime;
                if (!DateTime.TryParse(parsedConfig["STOPAT"], out stopAtDateTime))
                {
                    throw new ArgumentException(string.Format("db: .Net was unable determine the date and time of the stopat parameter: {0}", parsedConfig["STOPAT"]));
                }
                withOptions.Add("STOPAT=@stopat");
                param = new SqlParameter("@stopat", SqlDbType.DateTime);
                param.Value = stopAtDateTime;
                cmd.Parameters.Add(param);
            }
            parsedConfig.Remove("STOPAT");
            parsedArrayConfig.Remove("STOPAT");


            if (parsedConfig.ContainsKey("PARTIAL"))
            {
                withOptions.Add("PARTIAL");
            }
            parsedConfig.Remove("PARTIAL");
            parsedArrayConfig.Remove("PARTIAL");


            if (parsedConfig.ContainsKey("PARTIAL"))
            {
                withOptions.Add("PARTIAL");
            }
            parsedConfig.Remove("PARTIAL");
            parsedArrayConfig.Remove("PARTIAL");




            if (parsedConfig.ContainsKey("READ_WRITE_FILEGROUPS"))
            {
                filegroupOptions.Add("READ_WRITE_FILEGROUPS");
            }
            parsedConfig.Remove("READ_WRITE_FILEGROUPS");
            parsedArrayConfig.Remove("READ_WRITE_FILEGROUPS");

            if (parsedArrayConfig.ContainsKey("FILE"))
            {
                int i = 0;
                foreach (string file in parsedArrayConfig["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = file;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }
            parsedConfig.Remove("FILE");
            parsedArrayConfig.Remove("FILE");


            if (parsedArrayConfig.ContainsKey("FILEGROUP"))
            {
                int i = 0;
                foreach (string filegroup in parsedArrayConfig["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = filegroup;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }
            parsedConfig.Remove("FILEGROUP");
            parsedArrayConfig.Remove("FILEGROUP");


            if (parsedConfig.ContainsKey("LOADHISTORY"))
            {
                withOptions.Add("LOADHISTORY");
            }
            parsedConfig.Remove("LOADHISTORY");
            parsedArrayConfig.Remove("LOADHISTORY");



            if (parsedArrayConfig.ContainsKey("MOVE"))
            {
                string moveClause = " ";
                int i = 0;
                foreach (string moveInfo in parsedArrayConfig["MOVE"])
                {
                    if (i > 0)
                    {
                        moveClause += ", ";
                    }

                    int quoteCount = 0;
                    foreach (char c in moveInfo)
                    {
                        if (c == '\'') {
                            quoteCount++;
                        }
                    }
                    if (quoteCount != 4)
                    {
                        throw new ArgumentException(string.Format("db: Invalid MOVE clause: {0}.  Please write it in the form MOVE='from'TO'to'", moveInfo));
                    }

                    string[] moveSplit = moveInfo.Split('\'');
                    string moveFrom = moveSplit[1];
                    string moveToKeyword = moveSplit[2].Trim();;
                    string moveTo = moveSplit[3];

                    if (moveToKeyword != "TO")
                    {
                        throw new ArgumentException(string.Format("db: Invalid MOVE clause: {0}.  Please write it in the form MOVE='from'TO'to'", moveInfo));
                    }

                    

                    moveClause += string.Format("MOVE '{0}' TO '{1}'", moveFrom, moveTo);

                    i++;
                }
                withOptions.Add(moveClause);
            }
            parsedConfig.Remove("MOVE");
            parsedArrayConfig.Remove("MOVE");












            string filegroupClause = null;
            if (filegroupOptions.Count > 0)
            {
                for (int i = 0; i < filegroupOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        filegroupClause += ",";
                    }
                    filegroupClause += filegroupOptions[i];
                }
                filegroupClause += " ";
            }

            string withClause = null;
            if (withOptions.Count > 0)
            {
                withClause = " WITH ";
                for (int i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }


            string databaseOrLog = restoreType == RestoreType.Log ? "LOG" : "DATABASE";


            cmd.CommandType = CommandType.Text;
            cmd.CommandText = string.Format("RESTORE {0} @databasename {1}FROM VIRTUAL_DEVICE='{2}'{3};", databaseOrLog, filegroupClause, device, withClause);
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

        private enum BackupType
        {
            Full,
            Differential,
            Log
        }

        private enum RestoreType
        {
            Database,
            Log
        }
    }
}
