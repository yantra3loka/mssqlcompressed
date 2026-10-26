# SqlSqueeze (formerly MSSQL Compressed Backup)

[![CI](https://github.com/yantra3loka/sqlsqueeze/actions/workflows/ci.yml/badge.svg)](https://github.com/yantra3loka/sqlsqueeze/actions/workflows/ci.yml)

Open-source SQL Server backup compression via the Virtual Backup Device
Interface (VDI) — the same technique Redgate SQL Backup Pro uses, which is
why this works on editions (like Web/Express) where native
`BACKUP ... WITH COMPRESSION` is blocked.

Runs on **Windows and Linux** (`net10.0`, AnyCPU; SQL Server is 64-bit only
on both).

```text
sqlsqueeze backup [model] zstd file:///backups/model.bak.zst
sqlsqueeze backup [mydb] gzip aes(passwordEnv=BACKUP_KEY) file:///backups/mydb.bak.gz.aes
sqlsqueeze restore file:///backups/model.bak.zst zstd [model_restored]
sqlsqueeze prune "local(dir=/backups;pattern=*.zst;olderThanDays=30)" --json
sqlsqueeze verify file:///backups/model.bak.zst zstd
sqlsqueeze backup-all USER_DATABASES,-scratch* zstd "local(path=/backups/{database}/{database}_{backuptype}_{yyyyMMdd_HHmmss}.bak.zst)" --db "db(user=sa;passwordEnv=SQL_PW)"
```

Plugins: `db` (database) · `gzip` `bzip2` `zip64` `zstd` `aes` `rate`
(pipeline) · `local` `s3` `azure` (storage). Run `sqlsqueeze help`,
`sqlsqueeze listplugins`, `sqlsqueeze helpplugin <name>` for details.

## Backing up many databases

`sqlsqueeze backup-all <databases> [pipeline...] <storage> [--db "db(...)"]
[--type full|diff|log] [--no-fallback] [--json]` runs one backup per
database the first argument selects, instead of one command per database.

- **Selecting:** `USER_DATABASES`, `SYSTEM_DATABASES`, `ALL_DATABASES`,
  names, and `*`/`?` wildcards, comma separated and case-insensitive; a
  leading `-` excludes (`USER_DATABASES,-scratch*`). tempdb is never
  backed up; naming a database that does not exist is an error.
- **Paths:** `{database}` and `{backuptype}` in any storage parameter are
  replaced per database (`{database}` is required when more than one is
  selected, so backups cannot overwrite each other). Missing folders are
  created. Date tokens like `{yyyyMMdd_HHmmss}` work as before.
- **Log chains:** a differential or log backup that cannot be taken yet,
  because no full backup exists to build on, becomes a full backup (the
  result says so), or is skipped with `--no-fallback`. Log backups of
  SIMPLE-recovery databases, and any backup of a database that is not
  online, are skipped and reported. `master` is only ever backed up full.
- **Failures:** one failing database is reported and does not stop the
  rest; the exit code is `-1` if any failed. Skips are not failures.
- **Login:** `--db "db(user=sa;passwordEnv=SQL_PW;instancename=...)"` gives
  the same connection options as `backup`, without `database=`.

## Verifying a backup

`backup` to `local` storage writes `<file>.manifest.json` next to the
backup: database, backup type, the pipeline (secrets redacted; an
`passwordEnv=` variable name is kept, never its value), and the size and
SHA-256 of each file as stored and as SQL Server produced it. `prune`
removes a manifest together with its backup.

`sqlsqueeze verify <storage> [pipeline...]` reads the file back through the
pipeline (decrypt, decompress) and compares sizes and both hashes with the
manifest. It needs no SQL Server and can run on any machine that has the
file. Exit `0` when everything matches, `-1` otherwise; `--json` prints one
result line. It catches corruption, truncation, a wrong password, and a
pipeline that does not hand back the stream SQL Server wrote.

Add `--sql "db(user=sa;passwordEnv=SQL_PW)"` to also have SQL Server read
the whole backup: `RESTORE VERIFYONLY` through a virtual device, so the
pipeline is undone first and compressed or encrypted backups work. That
needs the SQL Server machine (and on Linux the `mssql` user), and is skipped
when the file already failed the checks above. It catches what the hashes
cannot: a file that decodes cleanly but is not a valid backup. Add
`;CHECKSUM` to the db options to also check page checksums; that only works
on a backup taken with `checksum` (SQL Server refuses it otherwise).

Without a manifest (`s3` and `azure` do not write one yet) the stream check
can only confirm the file decodes, and says so, because corruption can
decode cleanly (a flipped byte in a zstd file did).

## Scripting

- `--json` on backup/restore/prune: one result object on stdout, exit
  `0`/`-1`. See [tools/README.md](tools/README.md) for the contract.
- [PowerShell module](tools/SqlSqueeze.psm1): `Backup-SqlDatabase`,
  `Restore-SqlDatabase`, `Remove-SqlBackup`, `Test-SqlBackup`,
  `Test-SqlSqueezeBackup`, `Restore-SqlChain`.
- [SQL Agent templates](tools/sql-agent/) (CmdExec, Windows + Linux) and
  the [msdb dashboard view](tools/msdb/BackupHistory.sql).

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

## Download

Grab the latest `sqlsqueeze-win-x64.zip` / `sqlsqueeze-linux-x64.tar.gz`
from [Releases](https://github.com/yantra3loka/sqlsqueeze/releases) —
self-contained, no .NET install needed. (Linux: also build
`native/linux/libmsbpvdi.so` on the SQL box, see below.)

## Build / run (Windows or Linux)

```bash
dotnet build SqlSqueeze.slnx -c Release
dotnet test SqlSqueeze.slnx -c Release
dotnet SqlSqueeze.Cli/bin/Release/net10.0/sqlsqueeze.dll version
dotnet publish SqlSqueeze.Cli -c Release -o out
```

Linux additionally: build `native/linux/libmsbpvdi.so` (see above) and
place it next to the published binary or in `/opt/mssql/lib`.

## Roadmap — making it a true Redgate replacement

Done so far: .NET 10 port · Linux backend · `aes` + `zstd` plugins ·
`prune` retention · `--json` scripting API · PowerShell module · SQL Agent
templates · msdb dashboard · CI + binary releases · live validation on
Windows and Linux · `s3` + `azure` storage plugins · native-format
`RESTORE VERIFYONLY` Agent job · restore-chain helper (full → diff → logs,
NORECOVERY/STANDBY) · failure alerting (Agent operator + cron webhook) ·
write retry with backoff (`writeretries=`) on `local`, `s3` and `azure` ·
`verify` + backup manifests · `backup-all` (many databases, full/diff/log
planning).

Still to do, in order:

1. **Test restores.** `verify` proves the stored bytes are the bytes that
   were written, and `verify --sql` has SQL Server accept them. Still to
   do: a scheduled test-restore into a scratch database with
   `DBCC CHECKDB`, the only way to know a backup restores to a healthy
   database. The existing Agent verify job only covers native-format
   files.
2. **Self-describing backup files, the rest.** The manifest exists for
   `local` storage. Still to do: manifests for `s3`/`azure`, `restore`
   reading the pipeline from the manifest instead of needing it retyped,
   and a way to convert back to a native `.bak`.
3. **Broader live validation.** Windows is verified on SQL Server 2025
   Express and Linux on SQL Server 2022. Still to cover: 2016–2025 on
   both OSes, in CI.
4. **Resumable transfers, copy queue, real-cloud testing.** Writes retry
   with backoff within a run, but nothing resumes an interrupted upload
   in a later run and there is no copy-to/log-copy queue (Redgate keeps
   retrying for up to 24 hours). S3 and Azure were tested against moto
   and Azurite only, not real cloud accounts.
5. **Restore ergonomics.** Kill connections first, restore the latest
   chain automatically, report orphaned users, restore from cloud.

Non-goals: a GUI (the engine + scripts + docs slot into the Ola/dbatools
setups DBAs already run); anything that only works untested.

## Verification status

- `dotnet build -c Release`: 0 warnings, 0 errors (Linux).
- CLI `version` / `listplugins` / `help`: pass.
  (`sqlsqueeze v1.3.0.0 (.NET 10.0.13, X64)`, all 8 plugins found.)
- xUnit suite: **139/139 pass** — database selection, full/diff/log
  planning and the `backup-all` runner (fakes, no SQL Server), data
  source and `@@VERSION` parsing; manifest, verify (intact, flipped byte,
  truncated, wrong password, striped, no manifest) and every ordered pair
  of pipeline stages written through the real pipeline path; manifest-aware
  prune; pipeline round-trips (gzip, bzip2,
  zip64 named + first, zstd, rate, local, gzip→aes chain), AES (env key,
  random salt, wrong-password/truncated/missing-key failures, KDF golden
  vector), prune (age/pattern/defaults/validation, including S3 and Azure
  listings that end with an empty `NextContinuationToken`/`NextMarker`),
  date tokens, backend selection, restore byte counting, SQL login
  options, write retries, S3 and Azure storage against mocked services.
- Backend selection on Linux without SQL Server: factory picks the Linux
  backend and fails with the friendly missing-`libmsbpvdi.so` error:
  **PASS**. Struct/enum values asserted: **PASS**.
- Live against SQL Server 2025 Express (Windows VM, 2026-10-25): **PASS**.
  50k-row database: zstd full (13.9 MB → 1.0 MB, 0.4 s) and gzip+aes
  full restored with identical row counts + checksums; differential,
  log, and full→diff / full→log NORECOVERY chains all restore
  byte-correct (51k rows incl. chained diff). msdb history records every
  backup as `device_type 7` (virtual device). Restore order is the same
  as backup order (each layer inverts itself); VERIFYONLY/HEADERONLY
  reject compressed files by design, like Redgate `.sqb` files.
- Live against SQL Server 2022 on Linux (RTM-CU27, 16.0.4295.3,
  2026-10-26): **PASS**. A 20,000-row database backed up through the
  `native/linux` shim and restored (also with `MOVE` to a new database)
  with identical checksums. The first run exposed a heap corruption at
  the end of a working backup: the shim deleted the device object, which
  the device set owns. Fixed in `05e7aee`.
- S3 and Azure storage, live against emulators (2026-10-26): **PASS**.
  `s3` against moto: 6 MB multipart round trip byte-correct, prune
  verified. `azure` against Azurite: 3 MB in 3 blocks, SHA-256 identical
  after download. Not yet run against real S3 or Azure. Live runs found
  two real bugs that mocks missed: an Azure SharedKey canonical-resource
  signing error, and a prune loop that never ended because both services
  send an empty continuation element on the last page (fixed in
  `31099ca`).
- `verify` and manifests, live against SQL Server 2022 on Linux
  (2026-10-26): **PASS**. A 20,000-row database backed up with `zstd` and
  with `zstd` + `aes`; `verify` passed both against their manifests, failed
  a wrong AES key, failed a copy with one flipped byte (zstd decoded it
  without error, only the hashes caught it), and said so when the manifest
  was missing. The `zstd` + `aes` backup restored into a new database with
  identical row count and checksum.
- `backup-all`, live against SQL Server 2022 on Linux (2026-10-26):
  **PASS**. Five scratch databases (full recovery, simple recovery, new
  with no backup, offline, excluded) selected with `sqs_all_*`, so the
  instance's other databases were not touched. Full: three backed up into
  per-database folders with manifests, the offline one skipped, the excluded
  one untouched. Log: full-recovery databases got log backups, the simple one
  and the offline one were skipped, the new one fell back to a full with the
  reason reported; `--no-fallback` skipped it instead. Differential and
  fallback-to-full worked. With one database's folder made unwritable the
  others still completed and the exit code was `-1`. All 11 backups then
  passed `verify`.
- `verify --sql`, live against SQL Server 2022 on Linux (2026-10-26):
  **PASS**. `RESTORE VERIFYONLY` through VDI accepted `zstd` + `aes`
  backups (with and without page checksums, and `;CHECKSUM` on the one
  that had them); it refused `;CHECKSUM` on a backup without checksums
  with SQL Server's own message; it rejected a gzip file that decodes
  cleanly but is not a backup ("media family incorrectly formed"); and it
  was skipped for a file that had already failed the hash checks.
- **The VDI library prints an abort notice after the CLI's JSON line.**
  When SQL Server aborts a VDI operation, `libsqlvdi` writes a line like
  `Host_…_SQLVDIMemoryName_0: CVD::GetCommand: Abort bit is set` to stdout
  after the result, so "the last line" is not always the result. The cron
  wrapper and the PowerShell module took the last line, so a failed backup
  alerted with that notice as its message and a successful run could not
  be parsed. They now take the last line that is a JSON object, and CI runs
  tests for both (`tools/tests/`).
- **Differential backups failed on patched SQL Server builds.** The
  `@@VERSION` parser took the first `-` in the string, which on builds like
  `2022 (RTM-CU27)` or `2022 (RTM-GDR)` sits inside the parentheses, so a
  differential backup stopped with "unexpected version string". Fixed;
  covered by tests with real version strings.
- **Known fix for old backups:** before this change `zstd` did not close the
  stage after it, so any pipeline with a stage *after* `zstd` (for example
  `zstd aes(...)`) never flushed that stage and wrote a truncated,
  unrestorable file. `zstd` alone and `gzip aes(...)` were not affected.
  Found by `verify`; take new backups of anything made with
  `zstd` followed by another stage and do not trust the old ones.
- Other fixes from the same work: `local` storage opened backups for
  reading with write access, so another user's or read-only backup could
  not be restored or verified; and gzip, bzip2, zip64 and zstd rejected
  `level=` on restore, so one pipeline string could not be used for both
  backup and restore.
