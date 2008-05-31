using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Reflection;

using VirtualBackupDevice;

namespace MSSQLBackupPipe
{
    class DeviceThread : IDisposable
    {

        private bool mDisposed;
        private Stream mTopOfPipeline = null;
        private BackupDevice mDevice = null;
        private bool mIsBackup;
        private bool mFileExistsInitially;
        private FileInfo mFileInfo;
        private bool mCompletedSuccessfully = false;

        private Thread mThread;
        private Exception mException;

        public void PreConnect(bool isBackup, string deviceName, string filename, List<ConfigPair> pipelineConfig)
        {

            mIsBackup = isBackup;
            mFileInfo = new FileInfo(filename);
            mFileExistsInitially = mFileInfo.Exists;



            FileMode mode = isBackup ? FileMode.Create : FileMode.Open;

            mTopOfPipeline = new FileStream(filename, mode);

            mTopOfPipeline = CreatePipeline(pipelineConfig, mTopOfPipeline, isBackup);

            mDevice = new BackupDevice();

            mDevice.PreConnect(deviceName);
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
                mDevice.Connect(new TimeSpan(0, 0, 30));

                CommandBuffer buff = new CommandBuffer();

                try
                {
                    ReadWriteData(mDevice, buff, mTopOfPipeline, mIsBackup);
                }
                catch (Exception)
                {
                    mDevice.SignalAbort();
                    throw;
                }


                mCompletedSuccessfully = true;
            }
            catch (Exception e)
            {
                mException = e;
            }
        }



        private static void ReadWriteData(BackupDevice dev, CommandBuffer buff, Stream stream, bool isBackup)
        {
            while (dev.GetCommand(buff))
            {

                CompletionCode completionCode = CompletionCode.DISK_FULL;
                int bytesTransferred = 0;

                try
                {

                    switch (buff.GetCommandType())
                    {
                        case DeviceCommandType.Write:

                            if (!isBackup)
                            {
                                throw new InvalidOperationException("Cannot write in 'restore' mode");
                            }

                            stream.Write(buff.GetBuffer(), 0, buff.GetCount());
                            bytesTransferred = buff.GetCount();

                            completionCode = CompletionCode.SUCCESS;

                            break;
                        case DeviceCommandType.Read:

                            if (isBackup)
                            {
                                throw new InvalidOperationException("Cannot read in 'backup' mode");
                            }

                            byte[] buffArray = buff.GetBuffer();
                            bytesTransferred = stream.Read(buffArray, 0, buff.GetCount());
                            buff.SetBuffer(buffArray, bytesTransferred);

                            if (bytesTransferred > 0)
                            {
                                completionCode = CompletionCode.SUCCESS;
                            }
                            else
                            {
                                completionCode = CompletionCode.HANDLE_EOF;
                            }

                            break;
                        case DeviceCommandType.ClearError:
                            completionCode = CompletionCode.SUCCESS;
                            break;

                        case DeviceCommandType.Flush:
                            completionCode = CompletionCode.SUCCESS;
                            break;

                    }
                }
                finally
                {
                    dev.CompleteCommand(buff, completionCode, bytesTransferred);
                }


            }
        }



        private static Stream CreatePipeline(List<ConfigPair> pipelineConfig, Stream fileStream, bool isBackup)
        {
            Stream topStream = fileStream;

            for (int i = pipelineConfig.Count - 1; i >= 0; i--)
            {
                ConfigPair config = pipelineConfig[i];

                IBackupTransformer tran = config.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupTransformer;
                if (tran == null)
                {
                    throw new ArgumentException(string.Format("Unable to create pipe component: {0}", config.TransformationType.Name));
                }
                topStream = isBackup ? tran.GetBackupWriter(config.ConfigString, topStream) : tran.GetRestoreReader(config.ConfigString, topStream);
            }

            return topStream;
        }





        #region IDisposable Members

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        ~DeviceThread()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    // dispose of managed resources
                    if (mDevice != null)
                    {
                        mDevice.Dispose();
                        mDevice = null;


                        if (!mCompletedSuccessfully && mFileInfo != null)
                        {
                            if (mIsBackup && mFileInfo.Exists && !mFileExistsInitially)
                            {
                                mFileInfo.Delete();
                            }
                        }



                    }

                    if (mTopOfPipeline != null)
                    {
                        mTopOfPipeline.Dispose();
                        mTopOfPipeline = null;
                    }
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            mDisposed = true;

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            //base.Dispose(disposing);

        }


        #endregion
    }
}
