/* SqlSqueeze SQL Agent template: nightly FULL backup + retention.
 *
 * EDIT the four variables below, then run in SSMS (or sqlcmd) on the
 * instance that owns the database. Works on Windows and Linux Agent
 * (CmdExec steps only). {yyyyMMdd_HHmmss} is expanded by sqlsqueeze
 * itself, so no fragile %date% parsing.
 *
 * Chain: FULL (this job, nightly) -> DIFF (Diff-Backup-Job.sql, e.g.
 * every 4h) -> LOG (Log-Backup-Job.sql, e.g. every 15min).
 */
USE msdb;
GO

DECLARE @exe        nvarchar(4000) = N'C:\tools\sqlsqueeze\sqlsqueeze.exe'; -- or /opt/sqlsqueeze/sqlsqueeze
DECLARE @db         sysname        = N'MyDatabase';
DECLARE @backupDir  nvarchar(4000) = N'D:\Backups';                          -- or /var/backups
DECLARE @jobName    sysname        = N'SqlSqueeze - FULL - MyDatabase';

DECLARE @cmd nvarchar(4000) =
    N'"' + @exe + N'" backup "[' + @db + N']" zstd "local(path=' + @backupDir
    + N'\' + @db + N'_full_{yyyyMMdd_HHmmss}.bak.zst)" --json';
DECLARE @prune nvarchar(4000) =
    N'"' + @exe + N'" prune "local(dir=' + @backupDir
    + N';pattern=' + @db + N'_*.bak.zst;olderThanDays=14)" --json';

IF EXISTS (SELECT 1 FROM dbo.sysjobs WHERE name = @jobName)
    EXEC dbo.sp_delete_job @job_name = @jobName, @delete_unused_schedule = 1;

EXEC dbo.sp_add_job @job_name = @jobName, @enabled = 1,
    @description = N'SqlSqueeze compressed FULL backup + 14-day retention.';

EXEC dbo.sp_add_jobstep @job_name = @jobName, @step_name = N'Full backup',
    @subsystem = N'CmdExec', @command = @cmd,
    @on_success_action = 3, @on_fail_action = 2; -- go to prune / quit w/ failure

EXEC dbo.sp_add_jobstep @job_name = @jobName, @step_name = N'Retention prune',
    @subsystem = N'CmdExec', @command = @prune,
    @on_success_action = 1, @on_fail_action = 2;

-- Nightly 02:00.
EXEC dbo.sp_add_jobschedule @job_name = @jobName, @name = N'Nightly 02:00',
    @freq_type = 4, @freq_interval = 1, @active_start_time = 020000;
GO
