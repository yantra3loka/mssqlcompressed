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

using System;
using System.Collections.Generic;
using System.IO;

namespace SqlSqueeze.Plugins
{
    /// <summary>
    /// A writer that can be told its data is incomplete (the backup failed part-way), so it
    /// discards what it holds instead of committing it when it is disposed. Without this, a
    /// writer to cloud storage would complete the upload on dispose and leave a truncated
    /// object that looks like a backup.
    /// </summary>
    public interface IAbortableStream
    {
        void Abort();
    }

    public static class StreamAbort
    {
        /// <summary>Aborts every stream that can be aborted. One failing abort does not stop the next.</summary>
        public static void AbortAll(IEnumerable<Stream> streams)
        {
            foreach (Stream stream in streams)
            {
                IAbortableStream abortable = stream as IAbortableStream;
                if (abortable == null)
                {
                    continue;
                }
                try
                {
                    abortable.Abort();
                }
                catch (Exception)
                {
                    // Aborting is best effort: the caller is already handling the failure that led here.
                }
            }
        }
    }
}
