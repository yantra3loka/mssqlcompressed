using System;
using System.Collections.Generic;
using System.Text;

namespace MSBackupPipe.Common
{
    public class ParallelExecutionException : Exception 
    {
        private IList<Exception> mExceptions = new List<Exception>();

      
        public IList<Exception> Exceptions
        {
            get { return mExceptions; }
        }

        public bool HasExceptions
        {
            get
            {
                return mExceptions.Count > 0;
            }
        }
    }
}
