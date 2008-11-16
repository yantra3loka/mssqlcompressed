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


using namespace System;
using namespace System::Runtime::InteropServices;

namespace VirtualBackupDevice
{

	CommandBuffer::CommandBuffer(void)
	{
		mCmd = NULL;
		mCachedBuffer = gcnew array<unsigned char>(0);
	}

	void CommandBuffer::SetCommand(VDC_Command* cmd)
	{
		mCmd = cmd;



		if (mCachedBuffer->Length < (int)GetCount())
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

	void CommandBuffer::SetBuffer(array<unsigned char>^ buff, UINT32 count)
	{

		if (mCmd->size < (UINT32)count) 
		{
			throw gcnew System::ArgumentException("The buffer is too small.");
		}

		mCachedBuffer = buff;

		
		IntPtr buffIp(mCmd->buffer);

		Marshal::Copy(mCachedBuffer, 0, buffIp, count);

	}



	UINT32 CommandBuffer::GetCount()
	{
		return (UINT32)mCmd->size;
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
}


