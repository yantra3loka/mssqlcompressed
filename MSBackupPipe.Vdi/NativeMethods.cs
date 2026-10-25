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
using System.Runtime.InteropServices;

namespace VdiNet.VirtualBackupDevice
{
    /// <summary>
    /// Native VDI structures, interfaces and entry points.
    /// Translated from vdi.h / vdiguid.h of the Microsoft VDI specification.
    /// </summary>
    internal static class NativeMethods
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct VDConfig
    {
        public uint deviceCount;
        public uint features;
        public uint prefixZoneSize;
        public uint alignment;
        public uint softFileMarkBlockSize;
        public uint eomWarningSize;
        public uint serverTimeOut;
        public uint blockSize;
        public uint maxIODepth;
        public uint maxTransferSize;
        public uint bufferAreaSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VdcCommand
    {
        public uint commandCode;
        public uint size;
        public ulong position;
        public IntPtr buffer;
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
}
