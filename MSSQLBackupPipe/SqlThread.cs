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
        private string mDeviceName;
        private bool mIsBackup;
        private string mDatabaseName;
        private bool mCompletedSuccessfully;

        public void PreConnect(string databaseName, string deviceName, bool isBackup)
        {
            mDeviceName = deviceName;
            mIsBackup = isBackup;
            mDatabaseName = databaseName;
            mCnn = new SqlConnection("Data Source=.;Initial Catalog=master;Integrated Security=SSPI;");
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
                string sql;
                if (mIsBackup)
                {
                    sql = "BACKUP DATABASE [{0}] TO VIRTUAL_DEVICE='{1}';";
                }
                else
                {
                    sql = "RESTORE DATABASE [{0}] FROM VIRTUAL_DEVICE='{1}';";
                }
                sql = string.Format(sql, mDatabaseName, mDeviceName);

                using (SqlCommand cmd = new SqlCommand(sql))
                {
                    cmd.CommandTimeout = 0;
                    cmd.Connection = mCnn;
                    cmd.CommandType = CommandType.Text;

                    Console.WriteLine("Executing:");
                    Console.WriteLine(sql);

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
                    mCnn.Dispose();
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
