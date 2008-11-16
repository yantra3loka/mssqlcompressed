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



using namespace System;

namespace VirtualBackupDevice 
{
	public ref class VirtualDeviceSetConfig
	{
	public:
		VirtualDeviceSetConfig(void);

		UINT32 DeviceCount;
		UINT32 Features;
		UINT32 PrefixZoneSize;
		UINT32 Alignment;
		UINT32 SoftFileMarkBlockSize;
		UINT32 EomWarningSize;
		Nullable<TimeSpan> ServerTimeOut;


		void CopyTo(VDConfig* config);
		void CopyFrom(const VDConfig* config);
	};
}