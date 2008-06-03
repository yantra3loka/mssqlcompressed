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
#include "CommandBuffer.h"

#include "BackupDevice.h"

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace VirtualBackupDevice;

CommandBuffer::CommandBuffer(void)
{
	mCmd = NULL;
	mCachedBuffer = gcnew array<unsigned char>(0);
}

void CommandBuffer::SetCommand(VDC_Command* cmd)
{
	mCmd = cmd;



	if (mCachedBuffer->Length < GetCount())
	{
		mCachedBuffer = gcnew array<unsigned char>(cmd->size);
	}

	if (cmd->size > 0) 
	{
		IntPtr buffIp(cmd->buffer);

		Marshal::Copy(buffIp, mCachedBuffer, 0, cmd->size);
	}
}

array<unsigned char>^ CommandBuffer::GetBuffer()
{
	return mCachedBuffer;
}

void CommandBuffer::SetBuffer(array<unsigned char>^ buff, int count)
{

	if (mCmd->size < (DWORD)count) 
	{
		throw gcnew System::ArgumentException("The buffer is too small.");
	}

	mCachedBuffer = buff;

	
	IntPtr buffIp(mCmd->buffer);

	Marshal::Copy(mCachedBuffer, 0, buffIp, count);

}



int CommandBuffer::GetCount()
{
	return (int)mCmd->size;
}

DeviceCommandType CommandBuffer::GetCommandType()
{
	switch(mCmd->commandCode)
	{
	case VDC_Write:
		return DeviceCommandType::Write;
	case VDC_Read:
		return DeviceCommandType::Read;
	case VDC_Flush:
		return DeviceCommandType::Flush;
	case VDC_ClearError:
		return DeviceCommandType::ClearError;
	default:
		throw gcnew ArgumentException(String::Format("Unsupported command: {0}", mCmd->commandCode));
	}
}

VDC_Command* CommandBuffer::GetCommand()
{
	return mCmd;
}



BackupDevice::BackupDevice()
{
	mVd = NULL;
	mVds = NULL;
	mConfig = NULL;
}

BackupDevice::~BackupDevice()
{
	if (mVd != NULL)
	{
		mVd->Release();
	}

	if (mVds != NULL)
	{
		mVds->Close();
		mVds->Release();
	}


	Marshal::FreeHGlobal(mDeviceName);

	if (mConfig != NULL) 
	{
		delete mConfig;
	}

	mVd = NULL;
	mVds = NULL;
}



void BackupDevice::PreConnect(String ^deviceName)
{
	if (mVd != NULL || mVds != NULL)
	{
		throw gcnew System::InvalidProgramException(String::Format("You can only call Connect once."));
	}

	HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
	if (!SUCCEEDED(hr)) 
	{
		throw gcnew System::InvalidProgramException(String::Format("Failed to Coinit: x{0}", hr));
	}


	IClientVirtualDeviceSet2* vds;
	hr = CoCreateInstance(CLSID_MSSQL_ClientVirtualDeviceSet, NULL, CLSCTX_INPROC_SERVER, IID_IClientVirtualDeviceSet2, (void**)&vds);
	mVds = vds;
	if (!SUCCEEDED(hr)) 
	{
		throw gcnew System::InvalidProgramException(String::Format("Could not create an instance: CLSID_MSSQL_ClientVirtualDeviceSet, x{0}", hr));
	}


	mConfig = new VDConfig;

	// very simple config that is "pipe-like"
	memset(mConfig, 0, sizeof(*mConfig));  
	mConfig->deviceCount = 1;


	mDeviceName = Marshal::StringToHGlobalUni(deviceName);

	hr = mVds->CreateEx(NULL, (LPCWSTR)mDeviceName.ToPointer(), mConfig);
	if (!SUCCEEDED(hr)) 
	{
		throw gcnew System::InvalidProgramException(String::Format("VDS::Create failed: x{0}", hr));
	}
	

}

void BackupDevice::Connect(TimeSpan timeout)
{

	DWORD dwTimeout = (DWORD)timeout.TotalMilliseconds;

	HRESULT hr = mVds->GetConfiguration(dwTimeout, mConfig);
	if (!SUCCEEDED(hr)) 
	{
		throw gcnew System::InvalidProgramException(String::Format("timeout exceeded: x{0}", hr));
	}


	IClientVirtualDevice* vd;
	hr = mVds->OpenDevice((LPCWSTR)mDeviceName.ToPointer(), &vd);
	mVd = vd;
	if (!SUCCEEDED(hr)) 
	{
		throw gcnew System::InvalidProgramException(String::Format("VDS::OpenDevice failed: x{0}", hr));
	}


}

bool BackupDevice::GetCommand(CommandBuffer^ cBuff)
{
	VDC_Command* cmd;
	HRESULT hr = mVd->GetCommand(INFINITE, &cmd);
	if (SUCCEEDED(hr))
	{
		cBuff->SetCommand(cmd);
		return true;
	}
	else
	{
		if (hr == VD_E_CLOSE)
		{
			return false; // EOF
		}

		throw gcnew InvalidProgramException(String::Format("Unable to get the next command: {0}.", hr));
	}
	
}

void BackupDevice::CompleteCommand(CommandBuffer ^command, CompletionCode completionCode, int bytesTransferred)
{
	HRESULT hr;
	if (!SUCCEEDED(hr = mVd->CompleteCommand(command->GetCommand(), (DWORD)completionCode, (DWORD)bytesTransferred, 0)))
	{
		throw gcnew InvalidProgramException(String::Format("Unable to complete the command: {0}.", hr));
	}
}


void BackupDevice::SignalAbort()
{
	mVds->SignalAbort();
}

