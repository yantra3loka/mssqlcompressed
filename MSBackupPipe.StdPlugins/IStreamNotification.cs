using System;
using System.Collections.Generic;
using System.Text;

namespace MSBackupPipe.StdPlugins
{
    public interface IStreamNotification
    {
        /// <summary>
        /// The estimated size of all streams combined.  It does not need to be 100% accurate,
        /// as this is used for UI notification only, though users would like accuracy.
        /// </summary>
        long EstimatedBytes { get; set; }

        /// <summary>
        /// Gets the thread ID to use in the UpdateBytesProcessed method. 
        /// </summary>
        int GetThreadId();

        /// <summary>
        /// Whenever *any* stream reads or writes data, you must call this method
        /// so that the engine can keep track of the progress.
        /// </summary>
        /// <param name="additionalBytesProcessed"></param>
        /// <returns>The *suggested* duration to be notified again</returns>
        TimeSpan UpdateBytesProcessed(long totalBytesProcessedByThread, int threadId);

    }
}
