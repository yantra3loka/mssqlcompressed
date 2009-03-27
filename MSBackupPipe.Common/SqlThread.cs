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
        public List<string> PreConnect(string clusterNetworkName, string instanceName, string deviceSetName, int numDevices, IBackupDatabase dbComponent, string dbConfig, bool isBackup, IUpdateNotification notifier)
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
            }
            else
            {
                dbComponent.ConfigureRestoreCommand(dbConfig, deviceNames, mCmd);
            }

            return deviceNames;

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
