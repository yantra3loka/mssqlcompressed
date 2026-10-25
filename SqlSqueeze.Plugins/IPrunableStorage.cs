/*
	Copyright 2026 Krishnan Venkiteswaran

	This file is part of SqlSqueeze (formerly MSSQL Compressed Backup).

    SqlSqueeze is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;

namespace SqlSqueeze.Plugins
{
    public class PruneResult
    {
        public int DeletedFiles;
        public long BytesReclaimed;
        public List<string> DeletedPaths = new List<string>();
    }

    /// <summary>
    /// Optional capability for storage plugins: delete aged files.
    /// Only file-system-like plugins implement this.
    /// </summary>
    public interface IPrunableStorage
    {
        PruneResult PruneOldFiles(Dictionary<string, List<string>> config);
    }
}
