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
	//mVirtDevices.Clear();
	//mDeviceNames.Clear();
	mConfig = NULL;
}

BackupDevice::~BackupDevice()
{
	for(int i =0; i < mVirtDevices.Count; i++) 
	{
		IClientVirtualDevice* dev = (IClientVirtualDevice*)mVirtDevices[i].ToPointer();
		dev->Release();
	}
	mVirtDevices.Clear();


	if (mVds != NULL)
	{
		mVds->Close();
		mVds->Release();
	}

	for(int i =0; i < mDeviceNames.Count; i++) 
	{
		Marshal::FreeHGlobal(mDeviceNames[i]);
	}
	mDeviceNames.Clear();

	Marshal::FreeHGlobal(mInstanceName);

	if (mConfig != NULL) 
	{
		delete mConfig;
	}

	//mVd = NULL;
	mVds = NULL;
}



void BackupDevice::PreConnect(String ^instanceName, List<String^>^ deviceNames)
{
	if (mVirtDevices.Count > 0 || mVds != NULL)
	{
		throw gcnew System::InvalidProgramException(String::Format("You can only call Connect once."));
	}

	if (deviceNames->Count > 64) 
	{
		throw gcnew System::InvalidProgramException(String::Format("You can only have up to 64 devices."));
	}

	if (deviceNames->Count < 1) 
	{
		throw gcnew System::InvalidProgramException(String::Format("You must have at least one device."));
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
	mConfig->deviceCount = deviceNames->Count;

	for (int i = 0; i < deviceNames->Count; i++) 
	{
		mDeviceNames.Add(Marshal::StringToHGlobalUni(deviceNames[i]));
	}
	mInstanceName = IntPtr::Zero;
	if (!String::IsNullOrEmpty(instanceName)) 
	{
		mInstanceName = Marshal::StringToHGlobalUni(instanceName);
	}

	LPCWSTR instanceNamePtr = NULL;
	if (mInstanceName != IntPtr::Zero) 
	{
		instanceNamePtr = (LPCWSTR)mInstanceName.ToPointer();
	}

	hr = mVds->CreateEx(instanceNamePtr, (LPCWSTR)mDeviceNames[0].ToPointer(), mConfig);
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

	for (int i = 0; i < mDeviceNames.Count; i++) 
	{
		IClientVirtualDevice* vd;
		hr = mVds->OpenDevice((LPCWSTR)mDeviceNames[i].ToPointer(), &vd);
		if (!SUCCEEDED(hr)) 
		{
			throw gcnew System::InvalidProgramException(String::Format("VDS::OpenDevice failed: x{0}", hr));
		}
		IntPtr ptr(vd);
		mVirtDevices.Add(ptr);
	}

}

bool BackupDevice::GetCommand(int devicePos, CommandBuffer^ cBuff)
{
	if (devicePos < 0 || devicePos >= mDeviceNames.Count)
	{
		throw gcnew InvalidProgramException(String::Format("DevicePos must be 0 to [num devices] - 1: {0}.", devicePos));
	}

	VDC_Command* cmd;
	IClientVirtualDevice* vd = (IClientVirtualDevice*)mVirtDevices[devicePos].ToPointer();
	HRESULT hr = vd->GetCommand(INFINITE, &cmd);
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

void BackupDevice::CompleteCommand(int devicePos, CommandBuffer ^command, CompletionCode completionCode, int bytesTransferred)
{
	if (devicePos < 0 || devicePos >= mDeviceNames.Count)
	{
		throw gcnew InvalidProgramException(String::Format("DevicePos must be 0 to [num devices] - 1: {0}.", devicePos));
	}

	IClientVirtualDevice* vd = (IClientVirtualDevice*)mVirtDevices[devicePos].ToPointer();
	HRESULT hr;
	if (!SUCCEEDED(hr = vd->CompleteCommand(command->GetCommand(), (DWORD)completionCode, (DWORD)bytesTransferred, 0)))
	{
		throw gcnew InvalidProgramException(String::Format("Unable to complete the command: {0}.", hr));
	}
}


void BackupDevice::PreConnect(String ^instanceName, String^ deviceName)
{
	List<String^>^ devArray = gcnew List<String^>(1);
	devArray->Add(deviceName);
	PreConnect(instanceName, devArray);
}

bool BackupDevice::GetCommand(CommandBuffer^ cBuff)
{
	return GetCommand(0, cBuff);
}

void BackupDevice::CompleteCommand(CommandBuffer^ command, CompletionCode completionCode, int bytesTransferred)
{
	CompleteCommand(0, command, completionCode, bytesTransferred);
}


void BackupDevice::SignalAbort()
{
	mVds->SignalAbort();
}

