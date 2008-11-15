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


using namespace System;
using namespace System::Collections::Generic;


namespace VirtualBackupDevice 
{

	public ref class BackupDevice
	{
	public:
		BackupDevice();
		~BackupDevice();

		//old method.  Use the PreConnect() method with the list of device names.
		void PreConnect(String ^instanceName, String^ deviceName);
		// Call this method before executing your BACKUP or RESTORE command. The first device name must be globally unique (ideally a GUID).  The other device names must be unique in this list.
		void PreConnect(String ^instanceName, List<String^>^ deviceNames);
		// Call this method after you've sent the BACKUP or RESTORE command
		void Connect(TimeSpan timeout);
		//old method.  Use the GetCommand() method with the devicePos parameter.
		bool GetCommand(CommandBuffer^ cBuff);
		// Gets the data for the specified device.
		bool GetCommand(int devicePos, CommandBuffer^ cBuff);
		//old method.  Use the CompleteCommand() method with the devicePos parameter.
		void CompleteCommand(CommandBuffer^ command, CompletionCode completionCode, int bytesTransferred);
		// Must be called after each GetCommand() to report success or failure.  The same devicePos in GetCommand() should be used here.
		void CompleteCommand(int devicePos, CommandBuffer^ command, CompletionCode completionCode, int bytesTransferred);
		// Tells SQL Server to abort
		void SignalAbort();

	private:
		IClientVirtualDeviceSet2* mVds;
		IntPtr mInstanceName;
		VDConfig* mConfig;

		//int mNumDevices;
		List<IntPtr> mVirtDevices;
		//IClientVirtualDevice* mVd;
		List<IntPtr> mDeviceNames;
	};
}
