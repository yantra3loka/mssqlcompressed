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

#include "CommandBuffer.h"
#include "CompletionCode.h"

using namespace System;
using namespace System::Runtime::InteropServices;

namespace VirtualBackupDevice 
{
	public ref class VirtualDevice
	{
	public:


		// errors throw an exception
		// EOF returns false
		// timeouts return true and set timeOutOccurred to true
		bool GetCommand(Nullable<TimeSpan> timeOut, CommandBuffer^ cBuff, [Out] bool% timeOutOccurred);

		void CompleteCommand(CommandBuffer^ cBuff, CompletionCode completionCode, UINT32 dwBytesTransferred, UINT64 dwlPosition);

	internal:
		VirtualDevice(IClientVirtualDevice*);



	private:
		IClientVirtualDevice* mDevice;
	};
}