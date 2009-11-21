using System;
using System.Collections.Generic;
using System.Text;

using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    class CommandLineNotifier : IUpdateNotification
    {
        private bool mIsBackup;
        private DateTime mNextNotificationTimeUtc = DateTime.Today.AddDays(1);
        private DateTime mStartTime = DateTime.UtcNow;

        private readonly TimeSpan mMinTimeForUpdate = TimeSpan.FromSeconds(1.2);


        public CommandLineNotifier(bool isBackup)
        {
            mIsBackup = isBackup;
        }

        public void OnConnecting(string message)
        {
            // do nothing
        }

        public void OnStart()
        {
            lock (this)
            {
                Console.WriteLine(string.Format("{0} has started", mIsBackup ? "Backup" : "Restore"));
                mNextNotificationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(10));
                mStartTime = DateTime.UtcNow;
            }
        }

        public TimeSpan OnStatusUpdate(float percentComplete)
        {
            DateTime utcNow = DateTime.UtcNow;

            lock (this)
            {

                if (mNextNotificationTimeUtc < utcNow && utcNow - mStartTime > mMinTimeForUpdate)
                {
                    string percent = string.Format("{0:0.00}%", percentComplete * 100.0);
                    percent = new string(' ', 7 - percent.Length) + percent;
                    Console.Write(percent + " Complete. ");
                    DateTime estEndTime = mStartTime;
                    if (percentComplete > 0)
                    {
                        estEndTime = mStartTime.AddMilliseconds((utcNow - mStartTime).TotalMilliseconds / percentComplete);
                    }
                    Console.WriteLine(string.Format("Estimated End: {0} ", estEndTime));

                    TimeSpan nextWait = CalculateNextNotification(utcNow - mStartTime);
                    mNextNotificationTimeUtc = utcNow.Add(nextWait);
                    return nextWait;
                }
                if (((utcNow - mStartTime) - mMinTimeForUpdate) > utcNow - mNextNotificationTimeUtc)
                {
                    return mMinTimeForUpdate;
                }
                else
                {
                    return (utcNow - mNextNotificationTimeUtc);
                }
            }


        }

        /// <summary>
        /// More updates at the beginning -- fewer updates as time goes on.
        /// </summary>
        /// <param name="elapsedTime">The total time elapsed from when the processing started</param>
        /// <returns>A timespan to wait until displaying the next update</returns>
        private static TimeSpan CalculateNextNotification(TimeSpan elapsedTime)
        {
            if (elapsedTime.TotalSeconds < 3)
            {
                return TimeSpan.FromSeconds(1.2);
            }
            else
            {
                double secondsDelay = elapsedTime.TotalSeconds * 0.4;
                return TimeSpan.FromSeconds(secondsDelay);
            }
        }

    }
}
