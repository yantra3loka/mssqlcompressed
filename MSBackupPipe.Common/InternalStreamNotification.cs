using System;
using System.Collections.Generic;
using System.Text;
using MSBackupPipe.StdPlugins;

namespace MSBackupPipe.Common
{
    internal class InternalStreamNotification : IStreamNotification
    {
        private readonly IUpdateNotification mExternalNotification;
        private long mEstimatedBytes = 1;
        private long mBytesProcessed = 0;

        public InternalStreamNotification(IUpdateNotification notification)
        {
            mExternalNotification = notification;
        }

        public long EstimatedBytes
        {
            get
            {
                lock (this)
                {
                    return mEstimatedBytes;
                }
            }
            set
            {
                lock (this)
                {
                    mEstimatedBytes = Math.Max(1, value);
                }
            }
        }

        public void UpdateBytesProcessed(int additionalBytesProcessed)
        {
            if (additionalBytesProcessed < 0)
            {
                throw new ArgumentException(string.Format("additionalBytesProcessed must be non-negative. value={0}", additionalBytesProcessed));
            }

            float bytesProcessed;
            float size;
            lock (this)
            {
                mBytesProcessed += additionalBytesProcessed;
                bytesProcessed = mBytesProcessed;
                size = mEstimatedBytes;
            }


            mExternalNotification.OnStatusUpdate(Math.Max(0f, Math.Min(1f, bytesProcessed / size)));
        }


    }
}
