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
using System.Runtime.InteropServices;

namespace VdiNet.VirtualBackupDevice
{
    /// <summary>
    /// Windows backend: talks to sqlvdi.dll through COM, mirroring the
    /// original C++/CLI wrapper call for call. Timeouts are milliseconds.
    /// Only constructed on Windows (see VirtualBackupDeviceFactory).
    /// </summary>
    internal static class WindowsInterop
    {
        // CLSID_MSSQL_ClientVirtualDeviceSet (== IID_IClientVirtualDeviceSet)
        internal static readonly Guid ClsidClientVirtualDeviceSet =
            new Guid("40700425-0080-11d2-851f-00c04fc21759");

        internal static readonly Guid IidClientVirtualDeviceSet2 =
            new Guid("d0e6eb07-7a62-11d2-8573-00c04fc21759");

        internal const uint ClsctxInprocServer = 0x1;
        internal const uint Infinite = 0xFFFFFFFF;

        // vdierror.h
        internal const uint VdETimeout = 0x80770003;
        internal const uint VdEClose = 0x8077000E;

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        internal static extern int CoCreateInstance(
            [In] ref Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    /// <summary>
    /// IClientVirtualDeviceSet2. vtable order must match vdi.h:
    /// IUnknown (3) + Create, GetConfiguration, OpenDevice, Close,
    /// SignalAbort, OpenInSecondary, GetBufferHandle, MapBufferHandle (8)
    /// + CreateEx, OpenInSecondaryEx (2).
    /// </summary>
    [ComImport]
    [Guid("d0e6eb07-7a62-11d2-8573-00c04fc21759")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClientVirtualDeviceSet2
    {
        [PreserveSig]
        int Create([MarshalAs(UnmanagedType.LPWStr)] string name, ref VDConfig cfg);

        [PreserveSig]
        int GetConfiguration(uint timeout, out VDConfig cfg);

        [PreserveSig]
        int OpenDevice([MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.Interface)] out IClientVirtualDevice device);

        [PreserveSig]
        int Close();

        [PreserveSig]
        int SignalAbort();

        [PreserveSig]
        int OpenInSecondary([MarshalAs(UnmanagedType.LPWStr)] string setName);

        [PreserveSig]
        int GetBufferHandle(IntPtr buffer, out uint bufferHandle);

        [PreserveSig]
        int MapBufferHandle(uint bufferHandle, out IntPtr buffer);

        [PreserveSig]
        int CreateEx(
            [MarshalAs(UnmanagedType.LPWStr)] string instanceName,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            ref VDConfig cfg);

        [PreserveSig]
        int OpenInSecondaryEx(
            [MarshalAs(UnmanagedType.LPWStr)] string instanceName,
            [MarshalAs(UnmanagedType.LPWStr)] string setName);
    }

    [ComImport]
    [Guid("40700424-0080-11d2-851f-00c04fc21759")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClientVirtualDevice
    {
        [PreserveSig]
        int GetCommand(uint timeout, out IntPtr command);

        [PreserveSig]
        int CompleteCommand(IntPtr command, uint completionCode, uint bytesTransferred, ulong position);
    }

    internal sealed class WindowsVirtualDeviceSet : IVirtualDeviceSet
    {
        private IClientVirtualDeviceSet2 _native;
        private bool _disposed;

        public WindowsVirtualDeviceSet()
        {
            Guid clsid = WindowsInterop.ClsidClientVirtualDeviceSet;
            Guid iid = WindowsInterop.IidClientVirtualDeviceSet2;
            int hr = WindowsInterop.CoCreateInstance(
                ref clsid, IntPtr.Zero, WindowsInterop.ClsctxInprocServer, ref iid, out object obj);
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
            // VDI wants the bare instance name: strip a LocalDB
            // "(localdb)\..." prefix, which is a client connection
            // concept, not a VDI instance name. LocalDB instances are not
            // registered as services, so sqlvdi cannot resolve their names:
            // bind the default (empty) VDI endpoint instead.
            string vdiInstance = instanceName ?? string.Empty;
            const string localDbPrefix = "(localdb)\\";
            if (vdiInstance.StartsWith(localDbPrefix, StringComparison.OrdinalIgnoreCase))
            {
                vdiInstance = string.Empty;
            }
            int hr = _native.CreateEx(vdiInstance, deviceSetName, ref native);
            Check(hr, "VDS::CreateEx");
        }

        public VirtualDeviceSetConfig GetConfiguration(TimeSpan? timeout)
        {
            EnsureNotDisposed();
            uint nativeTimeout = timeout.HasValue
                ? Convert.ToUInt32(timeout.Value.TotalMilliseconds)
                : WindowsInterop.Infinite;
            int hr = _native.GetConfiguration(nativeTimeout, out VDConfig native);
            Check(hr, "VDS::GetConfiguration");
            return VirtualDeviceSetConfig.FromNative(native);
        }

        public IVirtualDevice OpenDevice(string deviceName)
        {
            EnsureNotDisposed();
            int hr = _native.OpenDevice(deviceName, out IClientVirtualDevice device);
            Check(hr, "VDS::OpenDevice");
            return new WindowsVirtualDevice(device);
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
                throw new ObjectDisposedException(nameof(WindowsVirtualDeviceSet));
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

        ~WindowsVirtualDeviceSet()
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
                    if (OperatingSystem.IsWindows())
                    {
                        Marshal.ReleaseComObject(native);
                    }
                }
            }
        }
    }

    internal sealed class WindowsVirtualDevice : IVirtualDevice
    {
        private readonly IClientVirtualDevice _native;

        internal WindowsVirtualDevice(IClientVirtualDevice native)
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
                : WindowsInterop.Infinite;
            int hr = _native.GetCommand(nativeTimeout, out IntPtr native);
            if (hr >= 0)
            {
                cmd.SetCommand(native);
                cmd.TimedOut = false;
                return true;
            }
            if ((uint)hr == WindowsInterop.VdETimeout)
            {
                cmd.TimedOut = true;
                return true;
            }
            if ((uint)hr == WindowsInterop.VdEClose)
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
}
