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
using System.Threading;

using MSBackupPipe.StdPlugins;

namespace MSBackupPipe.Common
{
    class SqlThread : IDisposable
    {
        //private Exception mException;
        private SqlConnection mCnn;
        private SqlCommand mCmd;
        IAsyncResult mAsyncResult;
        private bool mDisposed;

        /// <summary>
        /// Returns the auto-generated device names
        /// </summary>
        public List<string> PreConnect(string clusterNetworkName, string instanceName, string deviceSetName, int numDevices, IBackupDatabase dbComponent, Dictionary<string, List<string>> dbConfig, bool isBackup, IUpdateNotification notifier, out long estimatedTotalBytes)
        {
            string serverConnectionName = clusterNetworkName == null ? "." : clusterNetworkName;
            string dataSource = string.IsNullOrEmpty(instanceName) ? serverConnectionName : string.Format(@"{0}\{1}", serverConnectionName, instanceName);
            string connectionString = string.Format("Data Source={0};Initial Catalog=master;Integrated Security=SSPI;Asynchronous Processing=true;", dataSource);

            notifier.OnConnecting(string.Format("Connecting: {0}", connectionString));

            mCnn = new SqlConnection(connectionString);
            mCnn.Open();

            List<string> deviceNames = new List<string>(numDevices);
            deviceNames.Add(deviceSetName);
            for (int i = 1; i < numDevices; i++)
            {
                deviceNames.Add(string.Format("dev{0}", i));
            }

            mCmd = new SqlCommand();
            mCmd.Connection = mCnn;
            mCmd.CommandTimeout = 0;
            if (isBackup)
            {
                dbComponent.ConfigureBackupCommand(dbConfig, deviceNames, mCmd);
                estimatedTotalBytes = CalculateEstimatedDatabaseSize(mCnn, dbConfig);
            }
            else
            {
                dbComponent.ConfigureRestoreCommand(dbConfig, deviceNames, mCmd);
                estimatedTotalBytes = 0;
            }

            return deviceNames;

        }

        private static long CalculateEstimatedDatabaseSize(SqlConnection cnn, Dictionary<string, List<string>> dbConfig)
        {

            string backupType = "full";
            if (dbConfig.ContainsKey("backuptype"))
            {
                backupType = dbConfig["backuptype"][0].ToLowerInvariant();
            }
            if (backupType != "full" && backupType != "differential" && backupType != "log")
            {
                throw new ArgumentException(string.Format("db: Unknown backuptype: {0}", backupType));
            }

            string databaseName = dbConfig["database"][0];
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentException(string.Format("db: database parameter required"));
            }


            // full backups can use sp_spaceused() to calculate the backup size: http://msdn.microsoft.com/en-us/library/ms188776.aspx
            // differential backup estimate is going to be very inaccurate right now
            if (backupType == "full" || backupType == "differential")
            {
                using (SqlCommand cmd = new SqlCommand(string.Format("use [{0}]; exec sp_spaceused", databaseName), cnn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    reader.NextResult();
                    reader.Read();
                    string sizeStr = reader.GetString(reader.GetOrdinal("reserved"));
                    if (sizeStr.Contains("KB"))
                    {
                        int pos = sizeStr.IndexOf("KB");
                        return long.Parse(sizeStr.Substring(0, pos)) * 1024L;
                    }
                    // I don't know if this will occur:
                    else if (sizeStr.Contains("MB"))
                    {
                        int pos = sizeStr.IndexOf("MB");
                        return long.Parse(sizeStr.Substring(0, pos)) * 1024L * 1024L;
                    }
                    else
                    {
                        throw new InvalidCastException(string.Format("Unknown units (usually this is KB): ", sizeStr));
                    }
                }

            }



            // differiential? DIFF_MAP? http://social.msdn.microsoft.com/Forums/en-SG/sqldisasterrecovery/thread/7a5ea034-9c5a-4531-a0b3-40f67c9cef4a



            // transaction log suggestions here: http://www.eggheadcafe.com/software/aspnet/32622031/how-big-will-my-backup-be.aspx
            if (backupType == "log")
            {

                string sql = @"
                        DECLARE  @t TABLE
                        (
	                        [Database Name] nvarchar(500),
	                        [Log Size (MB)] nvarchar(100),
	                        [Log Space Used (%)] nvarchar(100),
	                        Status nvarchar(20)

                        );

                        INSERT INTO @t
                        EXEC ('DBCC SQLPERF(logspace)')

                        select CAST(CAST([Log Size (MB)] as float) * CAST([Log Space Used (%)] AS float) / 100.0 * 1024.0 * 1024.0 AS bigint) AS LogUsed 
                        from @t
                        WHERE [Database Name] = '{0}';

                        ";

                using (SqlCommand cmd = new SqlCommand(string.Format(sql, databaseName), cnn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    return reader.GetInt64(0);
                }
            }



            throw new NotImplementedException();
        }
        public void BeginExecute()
        {
            mAsyncResult = mCmd.BeginExecuteNonQuery();
        }
        public Exception EndExecute()
        {
            try
            {
                mCmd.EndExecuteNonQuery(mAsyncResult);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }

        ~SqlThread()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    if (mCmd != null)
                    {
                        mCmd.Dispose();
                    }
                    mCmd = null;

                    if (mCnn != null)
                    {
                        mCnn.Dispose();
                    }
                    mCnn = null;

                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            mDisposed = true;

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            //base.Dispose(disposing);

        }

    }
}
