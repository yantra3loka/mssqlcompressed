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

namespace VdiNet.VirtualBackupDevice
{
    public class VirtualDeviceSetConfig
    {
        public VirtualDeviceSetConfig()
        {
            DeviceCount = 0;
            Features = FeatureSet.PipeLike;
            PrefixZoneSize = 0;
            Alignment = 0;
            SoftFileMarkBlockSize = 0;
            EomWarningSize = 0;
            ServerTimeout = null;
        }

        public uint DeviceCount { get; set; }
        public FeatureBits Features { get; set; }
        public uint PrefixZoneSize { get; set; }
        public uint Alignment { get; set; }
        public uint SoftFileMarkBlockSize { get; set; }
        public uint EomWarningSize { get; set; }
        public TimeSpan? ServerTimeout { get; set; }

        internal VDConfig ToNative()
        {
            return new VDConfig
            {
                deviceCount = DeviceCount,
                features = (uint)Features,
                prefixZoneSize = PrefixZoneSize,
                alignment = Alignment,
                softFileMarkBlockSize = SoftFileMarkBlockSize,
                eomWarningSize = EomWarningSize,
                serverTimeOut = ServerTimeout.HasValue
                    ? Convert.ToUInt32(ServerTimeout.Value.TotalMilliseconds)
                    : 0,
                blockSize = 0,
                bufferAreaSize = 0,
                maxIODepth = 0,
                maxTransferSize = 0,
            };
        }

        internal static VirtualDeviceSetConfig FromNative(VDConfig config)
        {
            return new VirtualDeviceSetConfig
            {
                DeviceCount = config.deviceCount,
                Features = (FeatureBits)config.features,
                PrefixZoneSize = config.prefixZoneSize,
                Alignment = config.alignment,
                SoftFileMarkBlockSize = config.softFileMarkBlockSize,
                EomWarningSize = config.eomWarningSize,
                ServerTimeout = config.serverTimeOut == 0
                    ? (TimeSpan?)null
                    : TimeSpan.FromMilliseconds(Convert.ToDouble(config.serverTimeOut)),
            };
        }
    }
}
