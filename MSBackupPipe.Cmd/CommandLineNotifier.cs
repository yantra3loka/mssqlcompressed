using System;
using System.Collections.Generic;
using System.Text;

using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    class CommandLineNotifier :IUpdateNotification
    {
        private bool mIsBackup;
        private DateTime mNextNotificationTimeUtc = DateTime.Today;
        private DateTime mStartTime = DateTime.UtcNow;

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
                mNextNotificationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
                mStartTime = DateTime.UtcNow;
            }
        }

        public void OnStatusUpdate(float percentComplete)
        {
            DateTime utcNow = DateTime.UtcNow;
            lock (this)
            {
                if (mNextNotificationTimeUtc < utcNow)
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

                    mNextNotificationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
                }
            }
        }

    }
}
