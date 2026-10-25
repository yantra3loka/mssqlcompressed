/* SqlSqueeze SQL Agent template: DIFFERENTIAL backups (e.g. every 4 hours).
 * Same pattern as Full-Backup-Job.sql; see it for the full notes.
 * Requires the FULL job's base backup; restores need FULL + latest DIFF.
 */
USE msdb;
GO

DECLARE @exe        nvarchar(4000) = N'C:\tools\sqlsqueeze\sqlsqueeze.exe';
DECLARE @db         sysname        = N'MyDatabase';
DECLARE @backupDir  nvarchar(4000) = N'D:\Backups';
DECLARE @jobName    sysname        = N'SqlSqueeze - DIFF - MyDatabase';

DECLARE @cmd nvarchar(4000) =
    N'"' + @exe + N'" backup "db(database=' + @db + N';backuptype=differential)" zstd "local(path='
    + @backupDir + N'\' + @db + N'_diff_{yyyyMMdd_HHmmss}.bak.zst)" --json';

IF EXISTS (SELECT 1 FROM dbo.sysjobs WHERE name = @jobName)
    EXEC dbo.sp_delete_job @job_name = @jobName, @delete_unused_schedule = 1;

EXEC dbo.sp_add_job @job_name = @jobName, @enabled = 1,
    @description = N'SqlSqueeze compressed DIFFERENTIAL backup.';

EXEC dbo.sp_add_jobstep @job_name = @jobName, @step_name = N'Diff backup',
    @subsystem = N'CmdExec', @command = @cmd,
    @on_success_action = 1, @on_fail_action = 2;

-- Every 4 hours.
EXEC dbo.sp_add_jobschedule @job_name = @jobName, @name = N'Every 4 hours',
    @freq_type = 4, @freq_interval = 1, @freq_subday_type = 8,
    @freq_subday_interval = 4, @active_start_time = 0;
GO
