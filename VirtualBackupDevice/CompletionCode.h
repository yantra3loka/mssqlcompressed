/*
	Copyright 2008 Clay Lenhart <clay@lenharts.net>


	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
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


#pragma once

namespace VirtualBackupDevice 
{

	public enum class CompletionCode
	{
		SUCCESS = ERROR_SUCCESS,
		HANDLE_EOF = ERROR_HANDLE_EOF,
		DISK_FULL = ERROR_DISK_FULL,
		NOT_SUPPORTED  = ERROR_NOT_SUPPORTED,
		NO_DATA_DETECTED = ERROR_NO_DATA_DETECTED,
		FILEMARK_DETECTED = ERROR_FILEMARK_DETECTED,
		EOM_OVERFLOW = ERROR_EOM_OVERFLOW,
		END_OF_MEDIA = ERROR_END_OF_MEDIA,
		OPERATION_ABORTED = ERROR_OPERATION_ABORTED

	};
}