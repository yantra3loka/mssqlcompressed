using System;
using System.Collections.Generic;
using System.Text;

namespace MSSQLBackupPipe.Common
{
    public interface IUpdateNotification
    {
        /// <summary>
        /// Called several times when connecting to the various components
        /// </summary>
        void OnConnecting(string message);

        /// <summary>
        /// Called when everything is ready to backup or restore, and 
        /// nothing much can go wrong after this point.
        /// </summary>
        void OnStart();

        /// <summary>
        /// Updates the percent complete.
        /// </summary>
        /// <param name="percentComplete">A number from 0.0 to 1.0</param>
        void OnStatusUpdate(float percentComplete);

    }
}
