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
using System.IO;

namespace VdiNet.VirtualBackupDevice
{
    public interface IVirtualDeviceSet : IDisposable
    {
        void CreateEx(string instanceName, string deviceSetName, VirtualDeviceSetConfig config);
        VirtualDeviceSetConfig GetConfiguration(TimeSpan? timeout);
        IVirtualDevice OpenDevice(string deviceName);
        void SignalAbort();
        void Close();
    }

    public interface IVirtualDevice
    {
        ICommandBuffer CreateCommandBuffer();
        bool GetCommand(TimeSpan? timeout, ICommandBuffer buffer);
        void CompleteCommand(ICommandBuffer buffer, CompletionCode completionCode, uint bytesTransferred, ulong position);
    }

    public interface ICommandBuffer
    {
        bool TimedOut { get; }
        DeviceCommandType CommandType { get; }
        int WriteToStream(Stream destination);
        int ReadFromStream(Stream source);
    }

    public static class VirtualBackupDeviceFactory
    {
        /// <summary>
        /// SQL Server has been 64-bit only since 2016, so the platform is
        /// always x64. The parameters are kept so callers (and the plugin
        /// config surface) stay source-compatible. Note: SQL Server on
        /// Linux has no named instances; the backend ignores the instance
        /// name there.
        /// </summary>
        public static SqlPlatform DiscoverSqlPlatform(string instanceName, string clusterNetworkName)
        {
            return SqlPlatform.X64;
        }

        public static IVirtualDeviceSet NewVirtualDeviceSet(SqlPlatform platform)
        {
            if (platform != SqlPlatform.X64)
            {
                throw new PlatformNotSupportedException(
                    "Only 64-bit SQL Server is supported (SQL Server 2016 and later are 64-bit only).");
            }
            if (OperatingSystem.IsWindows())
            {
                return new WindowsVirtualDeviceSet();
            }
            if (OperatingSystem.IsLinux())
            {
                return new LinuxVirtualDeviceSet();
            }
            throw new PlatformNotSupportedException(
                string.Format("SqlSqueeze backups require Windows or Linux, not {0}.",
                    Environment.OSVersion.Platform));
        }
    }
}
