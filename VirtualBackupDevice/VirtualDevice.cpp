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

#include "StdAfx.h"
#include "VirtualDevice.h"

namespace VirtualBackupDevice 
{
	VirtualDevice::VirtualDevice(IClientVirtualDevice* dev)
	{
		mDevice = dev;
	}

	bool VirtualDevice::GetCommand(Nullable<TimeSpan> timeOut, CommandBuffer^ cBuff, [Out] bool% timeOutOccurred)
	{

		DWORD dwTimeOut = INFINITE;
		if (timeOut.HasValue) 
		{
			dwTimeOut = (DWORD)Convert::ToUInt64(timeOut.Value.TotalMilliseconds);
		}



		VDC_Command* cmd;

		HRESULT hr = mDevice->GetCommand(dwTimeOut, &cmd);
		if (SUCCEEDED(hr))
		{
			cBuff->SetCommand(cmd);
			timeOutOccurred = false;
			return true;
		}
		else if (hr == VD_E_TIMEOUT)
		{
			timeOutOccurred = true;
			return true;
		}
		else
		{
			if (hr == VD_E_CLOSE)
			{
				timeOutOccurred = false;
				return false; // EOF
			}

			throw gcnew InvalidProgramException(String::Format("Unable to get the next command: {0}.", hr));
		}
		


	}


	void VirtualDevice::CompleteCommand(CommandBuffer^ cBuff, CompletionCode completionCode, UINT32 dwBytesTransferred, UINT64 dwlPosition)
	{
		HRESULT hr;
		if (!SUCCEEDED(hr = mDevice->CompleteCommand(cBuff->GetCommand(), (UINT32)completionCode, dwBytesTransferred, dwlPosition)))
		{
			throw gcnew InvalidProgramException(String::Format("Unable to complete the command: {0}.", hr));
		}
	}


}
