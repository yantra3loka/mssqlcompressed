/* SqlSqueeze SQL Agent template: TRANSACTION LOG backups (e.g. every 15 min).
 * Same pattern as Full-Backup-Job.sql; see it for the full notes.
 * Database must be in FULL or BULK_LOGGED recovery. .trn files compress
 * extremely well; zstd level 1 keeps the 15-minute window cheap.
 */
USE msdb;
GO

DECLARE @exe        nvarchar(4000) = N'C:\tools\sqlsqueeze\sqlsqueeze.exe';
DECLARE @db         sysname        = N'MyDatabase';
DECLARE @backupDir  nvarchar(4000) = N'D:\Backups';
DECLARE @jobName    sysname        = N'SqlSqueeze - LOG - MyDatabase';

DECLARE @cmd nvarchar(4000) =
    N'"' + @exe + N'" backup "db(database=' + @db + N';backuptype=log)" zstd(level=1) "local(path='
    + @backupDir + N'\' + @db + N'_log_{yyyyMMdd_HHmmss}.trn.zst)" --json';

IF EXISTS (SELECT 1 FROM dbo.sysjobs WHERE name = @jobName)
    EXEC dbo.sp_delete_job @job_name = @jobName, @delete_unused_schedule = 1;

EXEC dbo.sp_add_job @job_name = @jobName, @enabled = 1,
    @description = N'SqlSqueeze compressed LOG backup.';

EXEC dbo.sp_add_jobstep @job_name = @jobName, @step_name = N'Log backup',
    @subsystem = N'CmdExec', @command = @cmd,
    @on_success_action = 1, @on_fail_action = 2;

-- Every 15 minutes.
EXEC dbo.sp_add_jobschedule @job_name = @jobName, @name = N'Every 15 minutes',
    @freq_type = 4, @freq_interval = 1, @freq_subday_type = 4,
    @freq_subday_interval = 15, @active_start_time = 0;
GO
