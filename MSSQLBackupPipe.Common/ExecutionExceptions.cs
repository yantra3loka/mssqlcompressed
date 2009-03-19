using System;
using System.Collections.Generic;
using System.Text;

namespace MSSQLBackupPipe.Common
{
    public class ExecutionExceptions : Exception 
    {
        private Exception mThreadException = null;
        private IList<Exception> mDeviceExceptions = new List<Exception>();

        public Exception ThreadException
        {
            get { return mThreadException; }
            set { mThreadException = value; }
        }
        public IList<Exception> DeviceExceptions
        {
            get { return mDeviceExceptions; }
        }

        public bool HasExceptions
        {
            get
            {
                return mThreadException != null || mDeviceExceptions.Count > 0;
            }
        }
    }
}
