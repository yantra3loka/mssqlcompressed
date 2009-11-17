using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSBackupPipe.StdPlugins
{
    public class TrackingStream : Stream
    {
        private Stream mSourceStream;
        private IStreamNotification mNotification;

        public TrackingStream(Stream source, IStreamNotification notification)
        {
            mSourceStream = source;
            mNotification = notification;
        }

        public override bool CanRead { get { return mSourceStream.CanRead; } }


        public override bool CanSeek { get { return false; } }



        public override bool CanTimeout { get { return mSourceStream.CanTimeout; } }


        public override bool CanWrite { get { return mSourceStream.CanWrite; } }

        public override long Length { get { return mSourceStream.Length; } }


        public override long Position { get { return mSourceStream.Position; } set { throw new NotSupportedException(); } }


        public override int ReadTimeout { get { return mSourceStream.ReadTimeout; } set { mSourceStream.ReadTimeout = value; } }


        public override int WriteTimeout { get { return mSourceStream.WriteTimeout; } set { mSourceStream.WriteTimeout = value; } }






        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return mSourceStream.BeginRead(buffer, offset, count, callback, state);
        }


        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            mNotification.UpdateBytesProcessed(count);
            return mSourceStream.BeginWrite(buffer, offset, count, callback, state);
        }


        public override void Close()
        {
            mSourceStream.Close();
        }






        protected override void Dispose(bool disposing)
        {
            mSourceStream.Dispose();
        }


        public override int EndRead(IAsyncResult asyncResult)
        {
            int bytesRead = mSourceStream.EndRead(asyncResult);
            mNotification.UpdateBytesProcessed(bytesRead);
            return bytesRead;
        }


        public override void EndWrite(IAsyncResult asyncResult)
        {
            mSourceStream.EndWrite(asyncResult);
        }


        public override void Flush()
        {
            mSourceStream.Flush();
        }



        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = mSourceStream.Read(buffer, offset, count);
            mNotification.UpdateBytesProcessed(bytesRead);
            return bytesRead;
        }




        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }


        public override void SetLength(long value)
        {
            mSourceStream.SetLength(value);
        }




        public override void Write(byte[] buffer, int offset, int count)
        {
            mSourceStream.Write(buffer, offset, count);
            mNotification.UpdateBytesProcessed(count);
        }


    }
}
