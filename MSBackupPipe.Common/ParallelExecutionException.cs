using System;
using System.Collections.Generic;
using System.Text;

namespace MSBackupPipe.Common
{
    public class ParallelExecutionException : Exception 
    {
        private Exception mThreadException;
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
