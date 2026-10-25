/* SqlSqueeze / native backup dashboard.
 * VDI backups (sqlsqueeze, Redgate, …) flow through BACKUP DATABASE, so
 * they land in msdb history like any other backup. device_type 7 =
 * virtual device. Run in msdb or any database with VIEW SERVER STATE.
 */
CREATE OR ALTER VIEW dbo.SqlSqueezeBackupHistory AS
SELECT
    s.database_name                                        AS [Database],
    CASE s.type WHEN 'D' THEN 'Full' WHEN 'I' THEN 'Diff'
               WHEN 'L' THEN 'Log' ELSE s.type END         AS [Type],
    s.backup_start_date                                    AS [Started],
    DATEDIFF(SECOND, s.backup_start_date, s.backup_finish_date)
                                                           AS [DurationSec],
    CAST(s.backup_size / 1048576.0 AS decimal(18, 1))      AS [SizeMB],
    CAST(s.compressed_backup_size / 1048576.0 AS decimal(18, 1))
                                                           AS [OnDiskMB],
    CASE WHEN s.backup_size > 0
         THEN CAST(100.0 * s.compressed_backup_size / s.backup_size
                   AS decimal(5, 1)) END                   AS [PctOfOriginal],
    CASE f.device_type
        WHEN 2 THEN 'Disk' WHEN 5 THEN 'Tape' WHEN 7 THEN 'VDI'
        WHEN 9 THEN 'URL' ELSE CAST(f.device_type AS varchar(8)) END
                                                           AS [Device],
    f.physical_device_name                                 AS [File],
    s.server_name                                          AS [Server],
    s.user_name                                            AS [ByUser]
FROM msdb.dbo.backupset AS s
JOIN msdb.dbo.backupmediafamily AS f
    ON s.media_set_id = f.media_set_id;
GO

/* Latest backup per database, newest first: */
-- SELECT TOP (50) * FROM dbo.SqlSqueezeBackupHistory ORDER BY Started DESC;

/* Anything older than your RPO still missing a fresh full: */
-- SELECT [Database], MAX(Started) AS LastFull
-- FROM dbo.SqlSqueezeBackupHistory WHERE [Type] = 'Full'
-- GROUP BY [Database] HAVING MAX(Started) < DATEADD(DAY, -1, SYSDATETIME());
