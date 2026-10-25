/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>
	.NET 10 port: VDI COM interop rewritten in C# (replaces the C++/CLI
	MSBackupPipe.VirtualBackupDevice project and the VDI.Net binary).

	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
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
    public sealed class VirtualDeviceSet : IVirtualDeviceSet
    {
        private IClientVirtualDeviceSet2 _native;
        private bool _disposed;

        public VirtualDeviceSet()
        {
            Guid clsid = NativeMethods.ClsidClientVirtualDeviceSet;
            Guid iid = NativeMethods.IidClientVirtualDeviceSet2;
            int hr = NativeMethods.CoCreateInstance(
                ref clsid, IntPtr.Zero, NativeMethods.ClsctxInprocServer, ref iid, out object obj);
            if (hr < 0)
            {
                throw new InvalidOperationException(
                    string.Format("Could not create an instance: CLSID_MSSQL_ClientVirtualDeviceSet, 0x{0:X8}. " +
                        "The SQL Server client components (sqlvdi) must be installed on this machine.", (uint)hr));
            }
            _native = (IClientVirtualDeviceSet2)obj;
        }

        public void CreateEx(string instanceName, string deviceSetName, VirtualDeviceSetConfig config)
        {
            EnsureNotDisposed();
            VDConfig native = config.ToNative();
            int hr = _native.CreateEx(instanceName ?? string.Empty, deviceSetName, ref native);
            Check(hr, "VDS::CreateEx");
        }

        public VirtualDeviceSetConfig GetConfiguration(TimeSpan? timeout)
        {
            EnsureNotDisposed();
            uint nativeTimeout = timeout.HasValue
                ? Convert.ToUInt32(timeout.Value.TotalMilliseconds)
                : NativeMethods.Infinite;
            int hr = _native.GetConfiguration(nativeTimeout, out VDConfig native);
            Check(hr, "VDS::GetConfiguration");
            return VirtualDeviceSetConfig.FromNative(native);
        }

        public IVirtualDevice OpenDevice(string deviceName)
        {
            EnsureNotDisposed();
            int hr = _native.OpenDevice(deviceName, out IClientVirtualDevice device);
            Check(hr, "VDS::OpenDevice");
            return new VirtualDevice(device);
        }

        public void SignalAbort()
        {
            EnsureNotDisposed();
            int hr = _native.SignalAbort();
            Check(hr, "VDS::SignalAbort");
        }

        public void Close()
        {
            EnsureNotDisposed();
            int hr = _native.Close();
            Check(hr, "VDS::Close");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed || _native == null)
            {
                throw new ObjectDisposedException(nameof(VirtualDeviceSet));
            }
        }

        private static void Check(int hr, string what)
        {
            if (hr < 0)
            {
                throw new InvalidOperationException(
                    string.Format("{0} failed: 0x{1:X8}", what, (uint)hr));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~VirtualDeviceSet()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                IClientVirtualDeviceSet2 native = _native;
                _native = null;
                _disposed = true;
                if (native != null)
                {
                    try
                    {
                        // Best effort: never let cleanup mask the real error.
                        native.Close();
                    }
                    catch (Exception)
                    {
                    }
                    Marshal.ReleaseComObject(native);
                }
            }
        }
    }

    public sealed class VirtualDevice : IVirtualDevice
    {
        private readonly IClientVirtualDevice _native;

        internal VirtualDevice(IClientVirtualDevice native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public ICommandBuffer CreateCommandBuffer()
        {
            return new CommandBuffer();
        }

        public bool GetCommand(TimeSpan? timeout, ICommandBuffer buffer)
        {
            CommandBuffer cmd = (CommandBuffer)buffer;
            uint nativeTimeout = timeout.HasValue
                ? Convert.ToUInt32(timeout.Value.TotalMilliseconds)
                : NativeMethods.Infinite;
            int hr = _native.GetCommand(nativeTimeout, out IntPtr native);
            if (hr >= 0)
            {
                cmd.SetCommand(native);
                cmd.TimedOut = false;
                return true;
            }
            if ((uint)hr == NativeMethods.VdETimeout)
            {
                cmd.TimedOut = true;
                return true;
            }
            if ((uint)hr == NativeMethods.VdEClose)
            {
                cmd.TimedOut = false;
                return false; // EOF
            }
            throw new InvalidOperationException(
                string.Format("Unable to get the next command: 0x{0:X8}.", (uint)hr));
        }

        public void CompleteCommand(ICommandBuffer buffer, CompletionCode completionCode, uint bytesTransferred, ulong position)
        {
            CommandBuffer cmd = (CommandBuffer)buffer;
            int hr = _native.CompleteCommand(cmd.Native, (uint)completionCode, bytesTransferred, position);
            if (hr < 0)
            {
                throw new InvalidOperationException(
                    string.Format("Unable to complete the command: 0x{0:X8}.", (uint)hr));
            }
        }
    }

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
            int count = checked((int)cmd.size);
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
            int wanted = checked((int)cmd.size);
            EnsureCache(wanted);
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
