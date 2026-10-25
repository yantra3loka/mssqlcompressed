# MSSQL Compressed Backup (.NET 10 port)

Open-source SQL Server backup compression via the Virtual Backup Device
Interface (VDI) — the same technique Redgate SQL Backup Pro uses, which is
why this works on editions (like Web/Express) where native
`BACKUP ... WITH COMPRESSION` is blocked.

```text
msbp.exe backup [model] gzip(level=5) file:///c:\model.bak.gz
msbp.exe restore file:///c:\model.bak.gz gzip [model]
```

Plugins: `db` (database) · `gzip` `bzip2` `zip64` `rate` (pipeline) ·
`local` (file storage). Run `msbp help`, `msbp listplugins`,
`msbp helpplugin <name>` for details.

## Provenance

Upstream: [MSSQL Compressed Backup](https://mssqlcompressed.sourceforge.net/)
by Clay Lenhart, mirrored here from SourceForge Mercurial (90 changesets,
2008–2011, tags `v1.1-*`/`v1.2-*`) plus the 1.2 release binaries tree.
License: GPL (see [COPYING.txt](COPYING.txt); per-file headers apply).
Upstream is abandoned — this repo keeps it building and running on modern
.NET and SQL Server.

## What changed in the .NET 10 port

- Targets `net10.0-windows`, x64 only (SQL Server 2016+ is 64-bit only).
  Must run **on the SQL Server machine** (VDI is local-only), Windows with
  .NET 10 runtime + SQL Server client components (`sqlvdi`).
- `MSBackupPipe.VirtualBackupDevice` (C++/CLI) **and** the `VdiNet`
  binary are replaced by `MSBackupPipe.Vdi`: pure-C# VDI COM interop
  (`VDC_Command`, `IClientVirtualDeviceSet2`, …) in the same
  `VdiNet.VirtualBackupDevice` namespace, so the pipeline code is untouched.
  Completion codes are Win32 codes per the VDI reference
  (`Success=0`, `HandleEof=38/ERROR_HANDLE_EOF`,
  `DiskFull=112/ERROR_DISK_FULL`).
- Handles the `VDC_Complete` command SQL Server 2016+ sends at end of
  stream (KB3188454) — the 2008-era code would have failed on it.
- `System.Data.SqlClient` → `Microsoft.Data.SqlClient` NuGet.
  Connection string drops obsolete `Asynchronous Processing=true` and adds
  `TrustServerCertificate=true` (new driver encrypts by default; matches
  old out-of-box local behavior). `SqlThread` uses `ExecuteNonQueryAsync`.
- SharpZipLib 0.85 DLL → SharpZipLib NuGet. Two real fixes from the
  upgrade: `GZipOutputStream` ctor no longer takes the level (uses
  `SetLevel`), and `Close()` inside `Dispose(bool)` recursed forever —
  both verified by round-trip tests (see below).
- `Restore` read loop keeps reading to the full VDI buffer instead of a
  single short `Stream.Read` (partial reads used to fake EOF).
- Dropped: `ReleasePackaging` (use `dotnet publish`), strong-name `.snk`
  signing, `Binaries/` redist DLLs, legacy FxCop suppressions.

## Build / run

```powershell
dotnet build MSSQLBackupPipe.slnx -c Release
dotnet MSBackupPipe.Cmd/bin/Release/net10.0-windows/msbp.dll version
dotnet publish MSBackupPipe.Cmd -c Release -o out
```

## Verification status

- `dotnet build -c Release`: 0 warnings, 0 errors.
- CLI `version` / `listplugins` / `help`: pass.
- 1 MiB round-trips (byte-identical): gzip, bzip2, zip64 (named + first
  entry), rate limiter, local storage: **ALL PASS**.
- Live `backup`/`restore` against SQL Server: **not yet run** — needs a
  Windows box with SQL Server. If you run one, please report back.
