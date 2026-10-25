# SqlSqueeze (formerly MSSQL Compressed Backup)

Open-source SQL Server backup compression via the Virtual Backup Device
Interface (VDI) — the same technique Redgate SQL Backup Pro uses, which is
why this works on editions (like Web/Express) where native
`BACKUP ... WITH COMPRESSION` is blocked.

Runs on **Windows and Linux** (`net10.0`, AnyCPU; SQL Server is 64-bit only
on both).

```text
sqlsqueeze backup [model] gzip(level=5) file:///backups/model.bak.gz
sqlsqueeze restore file:///backups/model.bak.gz gzip [model]
```

Plugins: `db` (database) · `gzip` `bzip2` `zip64` `rate` (pipeline) ·
`local` (file storage). Run `sqlsqueeze help`, `sqlsqueeze listplugins`,
`sqlsqueeze helpplugin <name>` for details.

## Provenance

Upstream: [MSSQL Compressed Backup](https://mssqlcompressed.sourceforge.net/)
by Clay Lenhart, mirrored here from SourceForge Mercurial (90 changesets,
2008–2011, tags `v1.1-*`/`v1.2-*`) plus the 1.2 release tree.
License: GPL (see [COPYING.txt](COPYING.txt); per-file headers apply).
Upstream is abandoned — this repo keeps it building and running on modern
.NET and SQL Server under the SqlSqueeze name.

## What changed

- Targets `net10.0`, OS-agnostic. Must run **on the SQL Server machine**
  (VDI is local-only) with the .NET 10 runtime.
- `MSBackupPipe.Vdi` (C# VDI interop, `VdiNet.VirtualBackupDevice`
  namespace) with two backends picked at runtime:
  - **Windows**: COM into `sqlvdi.dll` (ships with SQL Server).
  - **Linux**: P/Invoke into `native/linux/libmsbpvdi.so` (build it with
    `make` in `native/linux` — needs g++ and
    `/opt/mssql/lib/libsqlvdi.so` from SQL Server on Linux 2017+),
    because the Linux VDI API is C++ classes. Run as the `mssql` user
    (POSIX shm/sem permissions — see `native/linux/README.md`).
    Named instances don't exist on Linux: `instancename` is ignored there.
- Handles the `VDC_Complete` command SQL Server 2016+ sends at end of
  stream (KB3188454); other unknown commands are acknowledged
  `NotSupported` and skipped, mirroring Microsoft's own VDI sample —
  the 2008-era code aborted the whole backup on either.
- Completion codes are Win32 codes per the VDI reference
  (`Success=0`, `HandleEof=38/ERROR_HANDLE_EOF`,
  `DiskFull=112/ERROR_DISK_FULL`) — confirmed against Microsoft's
  [Linux VDI headers](https://github.com/Microsoft/sql-server-samples/tree/master/samples/features/sqlvdi-linux).
- `System.Data.SqlClient` → `Microsoft.Data.SqlClient` NuGet.
  Connection string drops obsolete `Asynchronous Processing=true` and adds
  `TrustServerCertificate=true` (new driver encrypts by default; matches
  old out-of-box local behavior). `SqlThread` uses `ExecuteNonQueryAsync`.
- SharpZipLib 0.85 DLL → SharpZipLib NuGet. Two real fixes from the
  upgrade: `GZipOutputStream` ctor no longer takes the level (uses
  `SetLevel`), and `Close()` inside `Dispose(bool)` recursed forever.
- `Restore` read loop keeps reading to the full VDI buffer instead of a
  single short `Stream.Read` (partial reads used to fake EOF).
- Dropped: `ReleasePackaging` (use `dotnet publish`), strong-name `.snk`
  signing, `Binaries/` redist DLLs, legacy FxCop suppressions.

## Build / run (Windows or Linux)

```bash
dotnet build SqlSqueeze.slnx -c Release
dotnet SqlSqueeze.Cli/bin/Release/net10.0/sqlsqueeze.dll version
dotnet publish SqlSqueeze.Cli -c Release -o out
```

Linux additionally: build `native/linux/libmsbpvdi.so` (see above) and
place it next to the published binary or in `/opt/mssql/lib`.

## Verification status

- `dotnet build -c Release`: 0 warnings, 0 errors (Linux).
- CLI `version` / `listplugins` / `help`: pass.
  (`sqlsqueeze v1.3.0.0 (.NET 10.0.13, X64)`, all 6 plugins found.)
- 1 MiB round-trips (byte-identical): gzip, bzip2, zip64 (named + first
  entry), rate limiter, local storage: **PASS**.
- Backend selection on Linux without SQL Server: factory picks the Linux
  backend and fails with the friendly missing-`libmsbpvdi.so` error:
  **PASS**. Struct/enum values asserted: **PASS**.
- Live `backup`/`restore` against SQL Server (either OS): **not yet run**.
  If you run one, please report back.
