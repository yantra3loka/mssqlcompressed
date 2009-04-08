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
	

	[Flags]
	public enum class Feature : int
	{
		Nothing = 0,
		Removable = VDF_Removable,
		FileMarks=VDF_FileMarks,
		RandomAccess =VDF_RandomAccess ,
		Rewind = VDF_Rewind,
		Position = VDF_Position,
		SkipBlocks = VDF_SkipBlocks,
		ReversePosition = VDF_ReversePosition,
		Discard = VDF_Discard,
		SnapshotPrepare = VDF_SnapshotPrepare,
		WriteMedia = VDF_WriteMedia,
		ReadMedia = VDF_ReadMedia
	};



	public ref class VirtualDeviceSetConfig
	{
	public:
		VirtualDeviceSetConfig(void);

		UINT32 DeviceCount;
		Feature Features;
		UINT32 PrefixZoneSize;
		UINT32 Alignment;
		UINT32 SoftFileMarkBlockSize;
		UINT32 EomWarningSize;
		Nullable<TimeSpan> ServerTimeOut;

		
	internal:
		void CopyTo(VDConfig* config);
		void CopyFrom(const VDConfig* config);
	};


	public ref class FeatureSet 
	{
	public:
		static Feature PipeLike = Feature::Nothing;
		static Feature TapeLike = Feature::FileMarks | Feature::Removable | Feature::ReversePosition | Feature::Rewind | Feature::Position | Feature::SkipBlocks;
		static Feature DiskLike = Feature::RandomAccess;
	};
}