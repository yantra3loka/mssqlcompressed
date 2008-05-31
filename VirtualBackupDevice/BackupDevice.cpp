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


// This is the main DLL file.

#include "stdafx.h"

//
//#include "BackupDevice.h"

//using namespace VirtualBackupDevice;
//using namespace System;
//using namespace System::Runtime::InteropServices;

//
//BackupDevice::BackupDevice()
//{
//	mVd = NULL;
//	mVds = NULL;
//}
//
//BackupDevice::~BackupDevice()
//{
//	if (mVd != NULL)
//	{
//		mVd->Release();
//	}
//
//	if (mVds != NULL)
//	{
//		mVds->Close();
//		mVds->Release();
//	}
//
//	mVd = NULL;
//	mVds = NULL;
//}
//
//void BackupDevice::Connect(String ^deviceName, TimeSpan timeout)
//{
//	if (mVd != NULL || mVds != NULL)
//	{
//		throw gcnew System::InvalidProgramException(String::Format("You can only call Connect once."));
//	}
//
//	HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
//	if (!SUCCEEDED(hr)) 
//	{
//		throw gcnew System::InvalidProgramException(String::Format("Failed to Coinit: x{0}", hr));
//	}
//
//
//	IClientVirtualDeviceSet2* vds;
//	hr = CoCreateInstance(CLSID_MSSQL_ClientVirtualDeviceSet, NULL, CLSCTX_INPROC_SERVER, IID_IClientVirtualDeviceSet2, (void**)&vds);
//	mVds = vds;
//	if (!SUCCEEDED(hr)) 
//	{
//		throw gcnew System::InvalidProgramException(String::Format("Could not create an instance: CLSID_MSSQL_ClientVirtualDeviceSet, x{0}", hr));
//	}
//
//
//	// very simple config that is "pipe-like"
//	VDConfig config;
//	memset(&config, 0, sizeof(config));  
//	config.deviceCount = 1;
//
//
//	IntPtr lDeviceName = Marshal::StringToHGlobalUni(deviceName);
//
//	hr = mVds->CreateEx(NULL, (LPCWSTR)lDeviceName.ToPointer(), &config);
//	if (!SUCCEEDED(hr)) 
//	{
//		Marshal::FreeHGlobal(lDeviceName);
//		throw gcnew System::InvalidProgramException(String::Format("VDS::Create failed: x{0}", hr));
//	}
//	
//
//
//	//if (isInput) 
//	//{
//	//	printf("Ready for restore command, for example:\n");
//	//	wprintf(L"RESTORE DATABASE [database] FROM VIRTUAL_DEVICE='%s';\n", deviceName);
//	//}
//	//else 
//	//{
//	//	printf("Ready for backup command, for example:\n");
//	//	wprintf(L"BACKUP DATABASE [database] TO VIRTUAL_DEVICE='%s';\n", deviceName);
//	//}
//
//	DWORD dwTimeout = (DWORD)timeout.TotalMilliseconds;
//
//	hr = mVds->GetConfiguration(dwTimeout, &config);
//	if (!SUCCEEDED(hr)) 
//	{
//		Marshal::FreeHGlobal(lDeviceName);
//		throw gcnew System::InvalidProgramException(String::Format("timeout exceeded: x{0}", hr));
//	}
//
//
//	IClientVirtualDevice* vd;
//	hr = mVds->OpenDevice((LPCWSTR)lDeviceName.ToPointer(), &vd);
//	mVd = vd;
//	if (!SUCCEEDED(hr)) 
//	{
//		Marshal::FreeHGlobal(lDeviceName);
//		throw gcnew System::InvalidProgramException(String::Format("VDS::OpenDevice failed: x{0}", hr));
//	}
//
//	Marshal::FreeHGlobal(lDeviceName);
//
//}
//
//bool BackupDevice::GetCommand(CommandBuffer^ cBuff)
//{
//	VDC_Command* cmd;
//	HRESULT hr = mVd->GetCommand(INFINITE, &cmd);
//	if (SUCCEEDED(hr))
//	{
//		cBuff->SetCommand(cmd);
//		return true;
//	}
//	else
//	{
//		if (hr == VD_E_CLOSE)
//		{
//			return false; // EOF
//		}
//
//		throw gcnew InvalidProgramException(String::Format("Unable to get the next command: {0}.", hr));
//	}
//	
//}
//
//void BackupDevice::CompleteCommand(CommandBuffer ^command, CompletionCode completionCode)
//{
//	HRESULT hr;
//	if (!SUCCEEDED(hr = mVd->CompleteCommand(command->GetCommand(), (DWORD)completionCode, command->GetCommand()->size, 0)))
//	{
//		throw gcnew InvalidProgramException(String::Format("Unable to complete the command: {0}.", hr));
//	}
//}
//
