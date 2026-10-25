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
    /// Layout-identical on Windows (DWORD/DWORD/DWORDLONG/pointer) and
    /// Linux (int32/int32/int64/pointer): 24 bytes either way.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VdcCommand
    {
        public uint commandCode;
        public int size;
        public long position;
        public IntPtr buffer;
    }

    /// <summary>
    /// Eleven uint32 fields on both platforms (44 bytes).
    /// </summary>
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
}
