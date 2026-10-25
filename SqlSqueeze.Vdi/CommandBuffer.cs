/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>
	.NET 10 port: VDI interop rewritten in C#.

	This file is part of SqlSqueeze (formerly MSSQL Compressed Backup).

    SqlSqueeze is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VdiNet.VirtualBackupDevice
{
    /// <summary>
    /// Backend-agnostic command buffer. The native VDC_Command memory is
    /// owned by SQL Server and stays valid until CompleteCommand; we only
    /// copy in and out of it. Marshal.Copy works on Windows and Linux.
    /// </summary>
    public sealed class CommandBuffer : ICommandBuffer
    {
        private IntPtr _native = IntPtr.Zero;
        private byte[] _cache = Array.Empty<byte>();

        public bool TimedOut { get; internal set; }

        public DeviceCommandType CommandType
        {
            get
            {
                return (DeviceCommandType)Marshal.PtrToStructure<VdcCommand>(_native).commandCode;
            }
        }

        internal IntPtr Native
        {
            get { return _native; }
        }

        internal void SetCommand(IntPtr native)
        {
            _native = native;
        }

        public int WriteToStream(Stream destination)
        {
            VdcCommand cmd = Marshal.PtrToStructure<VdcCommand>(_native);
            int count = cmd.size;
            if (count < 0)
            {
                throw new InvalidOperationException("VDI returned a negative buffer size.");
            }
            EnsureCache(count);
            if (count > 0)
            {
                Marshal.Copy(cmd.buffer, _cache, 0, count);
                destination.Write(_cache, 0, count);
            }
            return count;
        }

        public int ReadFromStream(Stream source)
        {
            VdcCommand cmd = Marshal.PtrToStructure<VdcCommand>(_native);
            int wanted = cmd.size;
            if (wanted < 0)
            {
                throw new InvalidOperationException("VDI returned a negative buffer size.");
            }
            EnsureCache(wanted);
            // Loop to a full buffer: a single short Stream.Read must not
            // fake end-of-stream during restore.
            int count = 0;
            while (count < wanted)
            {
                int read = source.Read(_cache, count, wanted - count);
                if (read == 0)
                {
                    break;
                }
                count += read;
            }
            if (count > 0)
            {
                Marshal.Copy(_cache, 0, cmd.buffer, count);
            }
            return count;
        }

        private void EnsureCache(int minSize)
        {
            if (_cache.Length < minSize)
            {
                _cache = new byte[minSize];
            }
        }
    }
}
