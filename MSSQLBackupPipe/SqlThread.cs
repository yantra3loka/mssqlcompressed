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


namespace MSSQLBackupPipe
{
    class SqlThread : IDisposable
    {
        private Thread mThread;
        private Exception mException;
        private SqlConnection mCnn;
        private bool mDisposed;
        private string mSqlStatement;
        private bool mCompletedSuccessfully;

        public void PreConnect(string instanceName, string sqlStatement)
        {
            mSqlStatement = sqlStatement;
            string dataSource = string.IsNullOrEmpty(instanceName) ? "." : string.Format(@".\{0}", instanceName);
            mCnn = new SqlConnection(string.Format("Data Source={0};Initial Catalog=master;Integrated Security=SSPI;", dataSource));
            mCnn.Open();
        }
        public void ConnectInAnoterThread()
        {
            ThreadStart job = new ThreadStart(ThreadStart);
            mThread = new Thread(job);
            mThread.Start();
        }
        public Exception WaitForCompletion()
        {
            mThread.Join();
            return mException;
        }


        private void ThreadStart()
        {
            try
            {

                using (SqlCommand cmd = new SqlCommand(mSqlStatement))
                {
                    cmd.CommandTimeout = 0;
                    cmd.Connection = mCnn;
                    cmd.CommandType = CommandType.Text;


                    Console.WriteLine("Executing:");
                    Console.WriteLine(mSqlStatement);

                    cmd.ExecuteNonQuery();
                }

                mCompletedSuccessfully = true;
            }
            catch (Exception e)
            {
                mException = e;
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
