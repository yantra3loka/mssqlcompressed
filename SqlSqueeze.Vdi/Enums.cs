/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>
	.NET 10 port: VDI COM interop rewritten in C# (replaces the C++/CLI
	SqlSqueeze.VirtualBackupDevice project and the VDI.Net binary).

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

namespace VdiNet.VirtualBackupDevice
{
    /// <summary>
    /// Maps to VDFeatures in vdi.h.
    /// </summary>
    [Flags]
    public enum FeatureBits : uint
    {
        None = 0,
        Removable = 0x1,
        Rewind = 0x2,
        Position = 0x10,
        SkipBlocks = 0x20,
        ReversePosition = 0x40,
        Discard = 0x80,
        FileMarks = 0x100,
        RandomAccess = 0x200,
        SnapshotPrepare = 0x400,
        EnumFrozenFiles = 0x800,
        VssWriter = 0x1000,
        WriteMedia = 0x10000,
        ReadMedia = 0x20000,
        LatchStats = 0x80000000,
    }

    public static class FeatureSet
    {
        public const FeatureBits PipeLike = FeatureBits.None;
        public const FeatureBits TapeLike = FeatureBits.FileMarks | FeatureBits.Removable
            | FeatureBits.ReversePosition | FeatureBits.Rewind | FeatureBits.Position
            | FeatureBits.SkipBlocks;
        public const FeatureBits DiskLike = FeatureBits.RandomAccess;
    }

    /// <summary>
    /// Maps to VDCommands in vdi.h. Complete was added by SQL Server 2016
    /// (KB3188454); older servers never send it.
    /// </summary>
    public enum DeviceCommandType : uint
    {
        Read = 1,
        Write = 2,
        ClearError = 3,
        Rewind = 4,
        WriteMark = 5,
        SkipMarks = 6,
        SkipBlocks = 7,
        Load = 8,
        GetPosition = 9,
        SetPosition = 10,
        Discard = 11,
        Flush = 12,
        Snapshot = 13,
        MountSnapshot = 14,
        PrepareToFreeze = 15,
        FileInfoBegin = 16,
        FileInfoEnd = 17,
        GetError = 18,
        // Added by SQL Server 2016 (KB3188454); value confirmed by the
        // Linux VDI header in Microsoft's sql-server-samples.
        Complete = 19,
    }

    /// <summary>
    /// Completion codes are plain Win32 status codes (see
    /// IClientVirtualDevice::CompleteCommand in the VDI reference):
    /// Success = ERROR_SUCCESS, HandleEof = ERROR_HANDLE_EOF,
    /// DiskFull = ERROR_DISK_FULL.
    /// </summary>
    public enum CompletionCode : uint
    {
        Success = 0,
        HandleEof = 38,
        DiskFull = 112,
        // Used for commands we don't implement (mirrors Microsoft's own
        // VDI sample, which completes unknown commands this way).
        NotSupported = 50,
    }

    public enum SqlPlatform
    {
        X86,
        X64,
    }
}
