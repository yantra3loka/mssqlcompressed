/*
    This file is part of SqlSqueeze.

    SqlSqueeze is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    SqlSqueeze is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with SqlSqueeze.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SqlSqueeze.Plugins;

namespace SqlSqueeze.Core
{
    /// <summary>
    /// Lists the databases on the server the db plugin options point at, with
    /// the state backup-all needs to plan full, differential and log backups.
    /// Connects like a backup does (same login and instance options).
    /// </summary>
    public class SqlDatabaseCatalog : IDatabaseCatalog
    {
        private const string Query = @"
SELECT d.name,
       d.recovery_model_desc,
       d.state_desc,
       CASE WHEN r.last_log_backup_lsn IS NULL THEN 0 ELSE 1 END,
       CASE WHEN EXISTS (SELECT 1 FROM sys.master_files f
                         WHERE f.database_id = d.database_id AND f.file_id = 1
                           AND f.differential_base_lsn IS NOT NULL) THEN 1 ELSE 0 END
FROM sys.databases d
LEFT JOIN sys.database_recovery_status r ON r.database_id = d.database_id
ORDER BY d.name;";

        private readonly ConfigPair mDatabaseConfig;

        public SqlDatabaseCatalog(ConfigPair databaseConfig)
        {
            mDatabaseConfig = databaseConfig;
        }

        public List<DatabaseInfo> List()
        {
            IBackupDatabase plugin = mDatabaseConfig.TransformationType.GetConstructor(new System.Type[0]).Invoke(new object[0]) as IBackupDatabase;
            string dataSource = SqlThread.ResolveDataSource(
                plugin.GetClusterNetworkName(mDatabaseConfig.Parameters),
                plugin.GetInstanceName(mDatabaseConfig.Parameters));

            List<DatabaseInfo> databases = new List<DatabaseInfo>();
            using (SqlConnection cnn = new SqlConnection(SqlThread.BuildConnectionString(dataSource, mDatabaseConfig.Parameters)))
            {
                cnn.Open();
                using (SqlCommand cmd = new SqlCommand(Query, cnn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        databases.Add(new DatabaseInfo
                        {
                            Name = reader.GetString(0),
                            RecoveryModel = reader.GetString(1),
                            State = reader.GetString(2),
                            HasLogBase = reader.GetInt32(3) == 1,
                            HasDiffBase = reader.GetInt32(4) == 1,
                        });
                    }
                }
            }
            return databases;
        }
    }
}
