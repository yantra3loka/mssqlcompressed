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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VdiNet.VirtualBackupDevice
{
    /// <summary>
    /// Linux backend: talks to /opt/mssql/lib/libsqlvdi.so through the
    /// tiny C shim in native/linux (libmsbpvdi.so), because the Linux VDI
    /// API is C++ classes, which P/Invoke cannot call directly.
    /// Timeouts are seconds (time_t), unlike milliseconds on Windows.
    /// </summary>
    internal static class LinuxInterop
    {
        internal const string LibraryName = "msbpvdi";

        // vdierror.h (same values as Windows)
        internal const int VdETimeout = unchecked((int)0x80770003);
        internal const int VdEClose = unchecked((int)0x8077000E);

        private static bool _resolverSet;

        internal static void EnsureResolver()
        {
            if (_resolverSet)
            {
                return;
            }
            lock (typeof(LinuxInterop))
            {
                if (_resolverSet)
                {
                    return;
                }
                try
                {
                    NativeLibrary.SetDllImportResolver(
                        typeof(LinuxInterop).Assembly, Resolve);
                }
                catch (InvalidOperationException)
                {
                    // Resolver already set for this assembly.
                }
                _resolverSet = true;
            }
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != LibraryName)
            {
                return IntPtr.Zero;
            }
            string fileName = "libmsbpvdi.so";
            string local = Path.Combine(AppContext.BaseDirectory, fileName);
            if (NativeLibrary.TryLoad(local, out IntPtr handle))
            {
                return handle;
            }
            foreach (string dir in new[] { "/opt/mssql/lib", "/usr/lib", "/usr/local/lib" })
            {
                if (NativeLibrary.TryLoad(Path.Combine(dir, fileName), out handle))
                {
                    return handle;
                }
            }
            return IntPtr.Zero;
        }

        internal static void ThrowNativeMissing(Exception inner)
        {
            throw new InvalidOperationException(
                "Could not load libmsbpvdi.so (looked next to sqlsqueeze, " +
                "/opt/mssql/lib, /usr/lib, /usr/local/lib). Build it with " +
                "native/linux/Makefile on the SQL Server machine and make sure " +
                "/opt/mssql/lib/libsqlvdi.so exists. See native/linux/README.md.",
                inner);
        }

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_new")]
        internal static extern IntPtr SetNew();

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_free")]
        internal static extern void SetFree(IntPtr set);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_create")]
        internal static extern int SetCreate(IntPtr set,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref VDConfig cfg);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_get_configuration")]
        internal static extern int SetGetConfiguration(IntPtr set, long timeoutSecs, out VDConfig cfg);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_open_device")]
        internal static extern int SetOpenDevice(IntPtr set,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out IntPtr device);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_close")]
        internal static extern int SetClose(IntPtr set);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_set_signal_abort")]
        internal static extern int SetSignalAbort(IntPtr set);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_dev_get_command")]
        internal static extern int DevGetCommand(IntPtr device, long timeoutSecs, out IntPtr command);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_dev_complete_command")]
        internal static extern int DevCompleteCommand(IntPtr device, IntPtr command,
            int completionCode, ulong bytesTransferred, long position);

        [DllImport(LibraryName, EntryPoint = "msbp_vdi_dev_free")]
        internal static extern void DevFree(IntPtr device);
    }

    internal sealed class LinuxVirtualDeviceSet : IVirtualDeviceSet
    {
        private IntPtr _native = IntPtr.Zero;
        private readonly List<IntPtr> _devices = new List<IntPtr>();
        private bool _disposed;

        public LinuxVirtualDeviceSet()
        {
            LinuxInterop.EnsureResolver();
            try
            {
                _native = LinuxInterop.SetNew();
            }
            catch (DllNotFoundException e)
            {
                LinuxInterop.ThrowNativeMissing(e);
            }
            if (_native == IntPtr.Zero)
            {
                throw new InvalidOperationException("msbp_vdi_set_new returned null (out of memory?).");
            }
        }

        public void CreateEx(string instanceName, string deviceSetName, VirtualDeviceSetConfig config)
        {
            EnsureNotDisposed();
            // SQL Server on Linux has no named instances: only the device
            // set name is passed through.
            VDConfig native = config.ToNative();
            int rc;
            try
            {
                rc = LinuxInterop.SetCreate(_native, deviceSetName, ref native);
            }
            catch (DllNotFoundException e)
            {
                LinuxInterop.ThrowNativeMissing(e);
                return;
            }
            Check(rc, "VDS::Create");
        }

        public VirtualDeviceSetConfig GetConfiguration(TimeSpan? timeout)
        {
            EnsureNotDisposed();
            long secs = timeout.HasValue
                ? Math.Max(1L, (long)Math.Ceiling(timeout.Value.TotalSeconds))
                : long.MaxValue;
            int rc = LinuxInterop.SetGetConfiguration(_native, secs, out VDConfig native);
            Check(rc, "VDS::GetConfiguration");
            return VirtualDeviceSetConfig.FromNative(native);
        }

        public IVirtualDevice OpenDevice(string deviceName)
        {
            EnsureNotDisposed();
            int rc = LinuxInterop.SetOpenDevice(_native, deviceName, out IntPtr device);
            Check(rc, "VDS::OpenDevice");
            lock (_devices)
            {
                _devices.Add(device);
            }
            return new LinuxVirtualDevice(device);
        }

        public void SignalAbort()
        {
            EnsureNotDisposed();
            int rc = LinuxInterop.SetSignalAbort(_native);
            Check(rc, "VDS::SignalAbort");
        }

        public void Close()
        {
            EnsureNotDisposed();
            int rc = LinuxInterop.SetClose(_native);
            Check(rc, "VDS::Close");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed || _native == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(LinuxVirtualDeviceSet));
            }
        }

        private static void Check(int rc, string what)
        {
            if (rc < 0)
            {
                throw new InvalidOperationException(
                    string.Format("{0} failed: 0x{1:X8}", what, unchecked((uint)rc)));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LinuxVirtualDeviceSet()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                IntPtr native = _native;
                _native = IntPtr.Zero;
                if (native != IntPtr.Zero)
                {
                    try
                    {
                        // Best effort: never let cleanup mask the real error.
                        LinuxInterop.SetClose(native);
                    }
                    catch (Exception)
                    {
                    }
                    lock (_devices)
                    {
                        foreach (IntPtr device in _devices)
                        {
                            try
                            {
                                LinuxInterop.DevFree(device);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        _devices.Clear();
                    }
                    try
                    {
                        LinuxInterop.SetFree(native);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }

    internal sealed class LinuxVirtualDevice : IVirtualDevice
    {
        private readonly IntPtr _native;

        internal LinuxVirtualDevice(IntPtr native)
        {
            _native = native != IntPtr.Zero
                ? native
                : throw new ArgumentNullException(nameof(native));
        }

        public ICommandBuffer CreateCommandBuffer()
        {
            return new CommandBuffer();
        }

        public bool GetCommand(TimeSpan? timeout, ICommandBuffer buffer)
        {
            CommandBuffer cmd = (CommandBuffer)buffer;
            long secs = timeout.HasValue
                ? Math.Max(1L, (long)Math.Ceiling(timeout.Value.TotalSeconds))
                : long.MaxValue;
            int rc = LinuxInterop.DevGetCommand(_native, secs, out IntPtr native);
            if (rc >= 0)
            {
                cmd.SetCommand(native);
                cmd.TimedOut = false;
                return true;
            }
            if (rc == LinuxInterop.VdETimeout)
            {
                cmd.TimedOut = true;
                return true;
            }
            if (rc == LinuxInterop.VdEClose)
            {
                cmd.TimedOut = false;
                return false; // EOF
            }
            throw new InvalidOperationException(
                string.Format("Unable to get the next command: 0x{0:X8}.", unchecked((uint)rc)));
        }

        public void CompleteCommand(ICommandBuffer buffer, CompletionCode completionCode, uint bytesTransferred, ulong position)
        {
            CommandBuffer cmd = (CommandBuffer)buffer;
            int rc = LinuxInterop.DevCompleteCommand(_native, cmd.Native, (int)completionCode,
                bytesTransferred, unchecked((long)position));
            if (rc < 0)
            {
                throw new InvalidOperationException(
                    string.Format("Unable to complete the command: 0x{0:X8}.", unchecked((uint)rc)));
            }
        }
    }
}
