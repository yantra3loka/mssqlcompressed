//*********************************************************************
//                 Copyright (C) Microsoft Corporation.
//
// @File: vdi.h
// @Owner: <owner alias>
//
// Purpose:
//   <description>
//
// Notes:
//   <special-instructions>
//
//*********************************************************************
#ifndef __vdi_h__
#define __vdi_h__

#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#pragma pack(push, _vdi_h_)

#include <time.h>
#include <stdint.h>

// Errors that need to be passed back to SQL Server, originally defined
// in Windows.h or similar
//
#define ERROR_SUCCESS           0L
#define ERROR_ARENA_TRASHED     7L
#define ERROR_HANDLE_EOF        38L
#define ERROR_HANDLE_DISK_FULL  39L
#define ERROR_NOT_SUPPORTED     50L
#define ERROR_DISK_FULL         112L
#define ERROR_OPERATION_ABORTED 995L

#pragma pack(8)
struct VDConfig
{
    uint32_t deviceCount;
    uint32_t features;
    uint32_t prefixZoneSize;
    uint32_t alignment;
    uint32_t softFileMarkBlockSize;
    uint32_t EOMWarningSize;
    uint32_t serverTimeOut;
    uint32_t blockSize;
    uint32_t maxIODepth;
    uint32_t maxTransferSize;
    uint32_t bufferAreaSize;
};

enum VDFeatures
{   VDF_Removable   = 0x1,
    VDF_Rewind  = 0x2,
    VDF_Position    = 0x10,
    VDF_SkipBlocks  = 0x20,
    VDF_ReversePosition = 0x40,
    VDF_Discard = 0x80,
    VDF_FileMarks   = 0x100,
    VDF_RandomAccess    = 0x200,
    VDF_SnapshotPrepare = 0x400,
    VDF_EnumFrozenFiles = 0x800,
    VDF_VSSWriter   = 0x1000,
    VDF_RequestComplete = 0x2000,
    VDF_WriteMedia  = 0x10000,
    VDF_ReadMedia   = 0x20000,
    VDF_CompleteEnabled = 0x40000,
    VDF_LatchStats  = 0x80000000,
    VDF_LikePipe    = 0,
    VDF_LikeTape    =
        ( ( ( ( ( VDF_FileMarks | VDF_Removable )  | VDF_Rewind )  | VDF_Position )  |
            VDF_SkipBlocks )  | VDF_ReversePosition ),
    VDF_LikeDisk    = VDF_RandomAccess};

enum VDCommands
{   VDC_Read    = 1,
    VDC_Write   = ( VDC_Read + 1 ),
    VDC_ClearError  = ( VDC_Write + 1 ),
    VDC_Rewind  = ( VDC_ClearError + 1 ),
    VDC_WriteMark   = ( VDC_Rewind + 1 ),
    VDC_SkipMarks   = ( VDC_WriteMark + 1 ),
    VDC_SkipBlocks  = ( VDC_SkipMarks + 1 ),
    VDC_Load    = ( VDC_SkipBlocks + 1 ),
    VDC_GetPosition = ( VDC_Load + 1 ),
    VDC_SetPosition = ( VDC_GetPosition + 1 ),
    VDC_Discard = ( VDC_SetPosition + 1 ),
    VDC_Flush   = ( VDC_Discard + 1 ),
    VDC_Snapshot    = ( VDC_Flush + 1 ),
    VDC_MountSnapshot   = ( VDC_Snapshot + 1 ),
    VDC_PrepareToFreeze = ( VDC_MountSnapshot + 1 ),
    VDC_FileInfoBegin   = ( VDC_PrepareToFreeze + 1 ),
    VDC_FileInfoEnd = ( VDC_FileInfoBegin + 1 ),
    VDC_GetError = (VDC_FileInfoEnd + 1),
    VDC_Complete = (VDC_GetError + 1)};

enum VDWhence
{   VDC_Beginning   = 0,
    VDC_Current = ( VDC_Beginning + 1 ),
    VDC_End = ( VDC_Current + 1 )};

struct VDC_Command
{
    int32_t commandCode;
    int32_t size;
    int64_t position;
    uint8_t* buffer;
};

struct VDS_Command
{
    int32_t commandCode;
    int32_t size;
    int64_t inPosition;
    int64_t outPosition;
    uint8_t* buffer;
    uint8_t* completionRoutine;
    uint8_t* completionContext;
    int32_t completionCode;
    int32_t bytesTransferred;
};

//----------------------------------------------------------------------------
// NAME: ClientVirtualDevice
//
// PURPOSE:
//
// Implement the ClientVirtualDevice component.
//
class CVDS;
class CVD;
class ClientVirtualDeviceSet;

class ClientVirtualDevice
{
public:
    ClientVirtualDevice();

    ~ClientVirtualDevice();

    int
    GetCommand(
        time_t        timeOut,
        VDC_Command** ppCmd);

    int
    CompleteCommand(
        VDC_Command*  pCmd,
        int           completionCode,
        unsigned long bytesTransferred,
        int64_t       position);

private:
    CVD* cvd;
    friend class ClientVirtualDeviceSet;
};

//----------------------------------------------------------------------------
// NAME: ClientVirtualDeviceSet
//
// PURPOSE:
//
// Implement the ClientVirtualDeviceSet component.
//

class ClientVirtualDeviceSet
{
public:
    int
    Create(
        char*     name,
        VDConfig* cfg);

    int
    GetConfiguration(
        time_t    timeout,
        VDConfig* cfg);

    int
    OpenDevice(
        char*                 name,
        ClientVirtualDevice** ppVirtualDevice);

    int
    Close();

    int
    SignalAbort();

    int
    OpenInSecondary(
        char* setName);

    int
    GetBufferHandle(
        uint8_t*      pBuffer,
        unsigned int* pBufferHandle);

    int
    MapBufferHandle(
        int       dwBuffer,
        uint8_t** ppBuffer);

    void
    RegisterDeviceClosed();

    ClientVirtualDeviceSet ();
    ~ClientVirtualDeviceSet ();

private:
    CVDS* cvds;
};

#pragma pack(pop, _vdi_h_)

#ifdef __cplusplus
}
#endif

#endif
