# SqlSqueeze operator scripts

## PowerShell module (`SqlSqueeze.psm1`)

```powershell
Import-Module ./tools/SqlSqueeze.psm1
$env:SQLSQUEEZE_EXE = 'C:\tools\sqlsqueeze\sqlsqueeze.exe'  # or put it on PATH

Backup-SqlDatabase -Database app -Path /backups/app.bak.zst
Backup-SqlDatabase -Database app -Path /backups/app.bak.gz.aes `
    -Transform @('gzip', 'aes(passwordEnv=BACKUP_KEY)') -BackupType Differential -Checksum
Restore-SqlDatabase -Path /backups/app.bak.zst -Database app_copy -Replace
Remove-SqlBackup -Directory /backups -Pattern '*.bak.zst' -OlderThanDays 30
Restore-SqlChain -Directory /backups -Database app -TargetDatabase app_copy
Restore-SqlChain -Directory /backups -Database app -TargetDatabase app_copy -StandbyFile /backups/undo.sby -WhatIf
Test-SqlBackup -Path /backups/app.bak.zst -ServerInstance .  # +VERIFYONLY when sqlcmd exists
Test-SqlSqueezeBackup -Path /backups/app.bak.zst             # hashes vs manifest, no SQL Server
```

Every call runs the CLI with `--json` and returns the result object;
failures throw with the server-side message.

## Object storage (`s3`)

`s3(endpoint=...;bucket=...;key=...;region=...;accesskey=...;secretkeyenv=...)`
stores backups in any S3-compatible service (AWS S3, MinIO, moto-tested).
Uploads always use multipart (buffered `partsizeMB`, default 8), so
arbitrarily large backups stream; the bucket is created when missing.
`key=` repeats for striped backups and takes `{date}` tokens. Prune with
`s3(endpoint=...;bucket=...;prefix=...;pattern=...;olderThanDays=...)`.
Signatures are SigV4 with no new dependencies.

`azure(endpoint=...;container=...;blob=...;account=...;accountkeyenv=...)`
stores backups in Azure Blob (or the Azurite emulator, tested live).
Uploads stage blocks (`blocksizeMB`, default 8) and commit a block list;
the container is created when missing. `blob=` repeats for striped
backups and takes `{date}` tokens. Prune mirrors `s3`. Auth is Storage
Shared Key with no new dependencies.

## Storage write retries

`local(path=...)` accepts `writeretries=N` (total attempts, default 1 =
no retry) and `writeretrybackoffms=M` (base backoff, default 1000,
doubling each attempt). Transient `IOException`s on write/flush are
retried; anything else fails fast. For dropped shares start with
`writeretries=5`.

## SQL Agent jobs (`sql-agent/`)

CmdExec-only templates (work on Windows and Linux Agent): edit the four
variables at the top of each file and run it. Full nightly + prune,
diff every 4h, log every 15min. Filenames use sqlsqueeze `{yyyyMMdd_HHmmss}`
tokens — no `%date%` parsing.

`Verify-Backup-Job.sql` runs a nightly `RESTORE VERIFYONLY` over the
newest native-format full backup — VERIFYONLY rejects compressed outputs
by design, so it targets native-format files. `Alert-Operator.sql` creates
the msdb operator and wires failure notifications onto the four backup
jobs (e-mail needs Database Mail configured on the instance).

## Cron wrapper (`cron/sqlsqueeze-cron.sh`)

Runs a backup from cron and POSTs a JSON webhook when it fails:

```bash
export WEBHOOK_URL='https://hooks.example.com/sqlsqueeze'
0 2 * * * /opt/sqlsqueeze/tools/cron/sqlsqueeze-cron.sh backup \
    "db(database=app)" zstd "local(path=/backups/app.bak.zst)"
```

Arguments go straight to the CLI (the wrapper appends `--json`), and the
CLI's exit code is the wrapper's exit code, so cron still sees the
failure. The webhook body is `{status, operation, exitCode, message,
timestamp, host}`. `WEBHOOK_URL` and the optional `WEBHOOK_TOKEN` (a
Bearer token) come from the environment — no URL, token or password is
stored in the script.

## msdb dashboard (`msdb/BackupHistory.sql`)

Creates `dbo.SqlSqueezeBackupHistory` (works for native backups too —
VDI backups log to `backupset` like everything else). Includes starter
queries for "latest per database" and "missing a fresh full".

## CLI scripting contract

- Exit codes: `0` success, `-1` any failure (backup/restore/prune).
- Append `--json` to `backup`, `restore` or `prune` for exactly one result
  line on stdout: `{status, operation, bytesProcessed?, estimatedBytes?,
  elapsedSeconds?, mbPerSecond?}` or `{status:"error", operation, message}`.
  Progress chatter is suppressed in `--json` mode, but other lines can still
  appear (`local:`, `zstd`, and the SQL Server VDI library's abort notice,
  which it prints *after* the result). Take the last line that starts with
  `{`, not simply the last line; the cron wrapper and PowerShell module do.
- `sqlsqueeze verify <storage> [pipeline...] [--sql "db(...)"] --json`
  prints `{status, operation:"verify", manifestFound, storedBytes,
  rawBytes, sqlChecked, sqlMessage, problems[]}`.
  `status` is `ok` only when `problems` is empty (exit `0`); otherwise
  `status:"error"`, `problems` lists what differs, exit `-1`. With
  `manifestFound:false` an `ok` only means the file decoded. With `--sql`,
  `sqlChecked` is true once SQL Server's `RESTORE VERIFYONLY` ran and
  `sqlMessage` is what it said (or why it failed). The cron
  wrapper reports `problems` as the webhook message, so it can run
  `verify` from cron: `sqlsqueeze-cron.sh verify file:///backups/app.bak.zst zstd`.
- `sqlsqueeze backup-all <databases> ... --json` prints `{status,
  operation:"backup-all", results:[{database, backupType, status,
  message, bytesProcessed, elapsedSeconds}]}`. Each result's `status` is
  `ok`, `skipped` (with the reason in `message`) or `error`; `backupType`
  is the type actually run, which differs from the requested one when a
  diff/log backup fell back to a full (`message` says so). The top-level
  `status` is `error` and the exit code `-1` if any database failed;
  skips do not count. Selecting nothing, or several databases into one
  path, is a top-level `{status:"error", message}` before any backup runs.
- `sqlsqueeze test-restore <storage> [pipeline...] --db "db(...)" --json`
  prints `{status, operation:"test-restore", scratchDatabase,
  restoredBytes, dropped, elapsedSeconds, problems[]}`. `status` is `ok`
  only when the backup restored, `DBCC CHECKDB` was clean and the scratch
  database was dropped (or `--keep` was given); otherwise `error` with
  `problems` and exit `-1`. A backup it cannot test (no manifest, not a
  full backup, no file list) is a top-level `{status:"error", message}`.
  Cron: `sqlsqueeze-cron.sh test-restore file:///backups/app.bak.zst zstd --db "db(user=sa;passwordEnv=SQL_PW)"`.
- `local` storage writes `<file>.manifest.json` beside each backup (database,
  type, pipeline with secrets redacted, sizes and SHA-256, and for
  backups of a database its files, so `test-restore` can place them) and
  `prune` removes it with the backup.
