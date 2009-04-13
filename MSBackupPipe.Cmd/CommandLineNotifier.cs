using System;
using System.Collections.Generic;
using System.Text;

using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    class CommandLineNotifier :IUpdateNotification
    {
        private bool mIsBackup;
        private float mPreviousHighestPercent;

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
                Console.WriteLine(string.Format("{0} is starting", mIsBackup ? "Backup" : "Restore"));
            }
        }

        public void OnStatusUpdate(float percentComplete)
        {
            lock (this)
            {
                int previousTenth = (int)(mPreviousHighestPercent * 10.0);
                int currentTenth = (int)(percentComplete * 10.0);

                if (previousTenth < currentTenth)
                {
                    Console.WriteLine(string.Format("{0}0% Complete", currentTenth));

                    mPreviousHighestPercent = percentComplete;
                }
            }
        }

    }
}
