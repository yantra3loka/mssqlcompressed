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


// VirtualBackupDevice.h

#pragma once






#include <objbase.h>
#pragma comment(lib, "ole32.lib")


//#include "vdi.h"
//#include "vdierror.h"
//#include "vdiguid.h"


#include "CommandBuffer.h"
#include "CompletionCode.h"
#include "INotifyWhenReady.h"


using namespace System;


namespace VirtualBackupDevice 
{

	public ref class BackupDevice
	{
	public:
		BackupDevice();
		~BackupDevice();
		void Connect(String^ deviceName, TimeSpan timeout, INotifyWhenReady^ notifyWhenReady);
		bool GetCommand(CommandBuffer^ cBuff);
		void CompleteCommand(CommandBuffer^ command, CompletionCode completionCode, int bytesTransferred);
		void SignalAbort();

	private:
		IClientVirtualDeviceSet2* mVds;
		IClientVirtualDevice* mVd;
	};
}
