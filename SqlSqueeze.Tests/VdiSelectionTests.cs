// Backend-selection and shared-constant checks. No SQL Server needed.

using System;
using VdiNet.VirtualBackupDevice;
using Xunit;

namespace SqlSqueeze.Tests
{
    public class VdiSelectionTests
    {
        [Fact]
        public void DiscoverSqlPlatform_ReturnsX64()
        {
            Assert.Equal(SqlPlatform.X64,
                VirtualBackupDeviceFactory.DiscoverSqlPlatform(null, null));
        }

        [Fact]
        public void CompletionCodes_MatchWin32()
        {
            // Win32 status codes per the VDI reference (IClientVirtualDevice::CompleteCommand).
            Assert.Equal(0u, (uint)CompletionCode.Success); // ERROR_SUCCESS
            Assert.Equal(38u, (uint)CompletionCode.HandleEof); // ERROR_HANDLE_EOF
            Assert.Equal(112u, (uint)CompletionCode.DiskFull); // ERROR_DISK_FULL
            Assert.Equal(50u, (uint)CompletionCode.NotSupported); // ERROR_NOT_SUPPORTED
        }

        [Fact]
        public void CompleteCommand_MatchesModernVdiHeader()
        {
            // VDC_Complete value from Microsoft's Linux VDI header (KB3188454).
            Assert.Equal(19u, (uint)DeviceCommandType.Complete);
        }

        [Fact]
        public void NewVirtualDeviceSet_PicksCurrentOsBackend()
        {
            // No SQL Server / native VDI libs on a build agent, so creating
            // the set must fail — but with the backend-specific friendly
            // error, proving the right backend was picked.
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
            {
                using (VirtualBackupDeviceFactory.NewVirtualDeviceSet(SqlPlatform.X64))
                {
                }
            });
            if (OperatingSystem.IsLinux())
            {
                Assert.Contains("libmsbpvdi.so", e.Message);
            }
            else if (OperatingSystem.IsWindows())
            {
                Assert.Contains("CLSID_MSSQL_ClientVirtualDeviceSet", e.Message);
            }
        }
    }
}
