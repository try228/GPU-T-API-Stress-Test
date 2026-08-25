using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Native.OneAPI;

/// <summary>
/// Native P/Invoke bindings for the Intel oneAPI Level Zero Core API (libze_loader.so.1).
/// </summary>
public static unsafe partial class OneApiNative
{
    private const string ZeLib = "libze_loader.so.1";

    /// <summary>Level Zero API success status code.</summary>
    public const int ZE_RESULT_SUCCESS = 0;
    
    // Standard Level Zero DDI structure type codes
    public const int ZE_STRUCTURE_TYPE_DRIVER_PROPERTIES = 0x0;
    public const int ZE_STRUCTURE_TYPE_DEVICE_PROPERTIES = 0x2;
    public const int ZE_STRUCTURE_TYPE_CONTEXT_DESC = 0xd;             // 13
    public const int ZE_STRUCTURE_TYPE_COMMAND_QUEUE_DESC = 0xe;       // 14
    public const int ZE_STRUCTURE_TYPE_COMMAND_LIST_DESC = 0xf;        // 15
    public const int ZE_STRUCTURE_TYPE_DEVICE_MEM_ALLOC_DESC = 0x15;   // 21
    public const int ZE_STRUCTURE_TYPE_MODULE_DESC = 0x1b;             // 27
    public const int ZE_STRUCTURE_TYPE_KERNEL_DESC = 0x1d;             // 29
    
    public const int ZE_MODULE_FORMAT_IL_SPIRV = 0x0;
    public const int ZE_MODULE_FORMAT_NATIVE = 0x1;

    [LibraryImport(ZeLib, EntryPoint = "zeInit")]
    public static partial int zeInit(uint flags);

    [LibraryImport(ZeLib, EntryPoint = "zeDriverGet")]
    public static partial int zeDriverGet(uint* count, nint* phDrivers);

    [LibraryImport(ZeLib, EntryPoint = "zeDeviceGet")]
    public static partial int zeDeviceGet(nint hDriver, uint* count, nint* phDevices);

    [LibraryImport(ZeLib, EntryPoint = "zeDeviceGetProperties")]
    public static partial int zeDeviceGetProperties(nint hDevice, ZeDeviceProperties* pProperties);

    [LibraryImport(ZeLib, EntryPoint = "zeContextCreate")]
    public static partial int zeContextCreate(nint hDriver, ZeContextDesc* desc, nint* phContext);

    [LibraryImport(ZeLib, EntryPoint = "zeContextDestroy")]
    public static partial int zeContextDestroy(nint hContext);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandQueueCreate")]
    public static partial int zeCommandQueueCreate(nint hContext, nint hDevice, ZeCommandQueueDesc* desc, nint* phCommandQueue);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandQueueDestroy")]
    public static partial int zeCommandQueueDestroy(nint hCommandQueue);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandListCreate")]
    public static partial int zeCommandListCreate(nint hContext, nint hDevice, ZeCommandListDesc* desc, nint* phCommandList);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandListDestroy")]
    public static partial int zeCommandListDestroy(nint hCommandList);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandListClose")]
    public static partial int zeCommandListClose(nint hCommandList);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandQueueExecuteCommandLists")]
    public static partial int zeCommandQueueExecuteCommandLists(nint hCommandQueue, uint numCommandLists, nint* phCommandLists, nint hFence);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandQueueSynchronize")]
    public static partial int zeCommandQueueSynchronize(nint hCommandQueue, ulong timeout);

    [LibraryImport(ZeLib, EntryPoint = "zeMemAllocDevice")]
    public static partial int zeMemAllocDevice(nint hContext, ZeDeviceMemAllocDesc* device_desc, nuint size, nuint alignment, nint hDevice, nint* pptr);

    [LibraryImport(ZeLib, EntryPoint = "zeMemFree")]
    public static partial int zeMemFree(nint hContext, nint ptr);

    [LibraryImport(ZeLib, EntryPoint = "zeModuleCreate")]
    public static partial int zeModuleCreate(nint hContext, nint hDevice, ZeModuleDesc* desc, nint* phModule, nint* phBuildLog);

    [LibraryImport(ZeLib, EntryPoint = "zeModuleDestroy")]
    public static partial int zeModuleDestroy(nint hModule);

    [LibraryImport(ZeLib, EntryPoint = "zeModuleBuildLogGetString")]
    public static partial int zeModuleBuildLogGetString(nint hBuildLog, nuint* pSize, byte* pBuildLog);

    [LibraryImport(ZeLib, EntryPoint = "zeModuleBuildLogDestroy")]
    public static partial int zeModuleBuildLogDestroy(nint hBuildLog);

    [LibraryImport(ZeLib, EntryPoint = "zeKernelCreate")]
    public static partial int zeKernelCreate(nint hModule, ZeKernelDesc* desc, nint* phKernel);

    [LibraryImport(ZeLib, EntryPoint = "zeKernelDestroy")]
    public static partial int zeKernelDestroy(nint hKernel);

    [LibraryImport(ZeLib, EntryPoint = "zeKernelSetArgumentValue")]
    public static partial int zeKernelSetArgumentValue(nint hKernel, uint argIndex, nuint argSize, void* pArgValue);

    [LibraryImport(ZeLib, EntryPoint = "zeKernelSetGroupSize")]
    public static partial int zeKernelSetGroupSize(nint hKernel, uint groupSizeX, uint groupSizeY, uint groupSizeZ);

    [LibraryImport(ZeLib, EntryPoint = "zeCommandListAppendLaunchKernel")]
    public static partial int zeCommandListAppendLaunchKernel(nint hCommandList, nint hKernel, ZeGroupCount* pLaunchFuncArgs, nint hSignalEvent, uint numWaitEvents, nint* phWaitEvents);
}

/// <summary>Level Zero context descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeContextDesc { public int stype; public void* pNext; public uint flags; }

/// <summary>Level Zero command queue descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeCommandQueueDesc { public int stype; public void* pNext; public uint ordinal; public uint index; public uint flags; public int mode; public int priority; }

/// <summary>Level Zero command list descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeCommandListDesc { public int stype; public void* pNext; public uint commandQueueGroupOrdinal; public uint flags; }

/// <summary>Level Zero device memory allocation descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeDeviceMemAllocDesc { public int stype; public void* pNext; public uint flags; public uint ordinal; }

/// <summary>Level Zero module creation descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeModuleDesc { public int stype; public void* pNext; public int format; public nuint inputSize; public byte* pInputModule; public byte* pBuildFlags; public void* pConstants; }

/// <summary>Level Zero kernel descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeKernelDesc { public int stype; public void* pNext; public uint flags; public byte* pKernelName; }

/// <summary>Level Zero kernel thread group dispatch dimensions.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct ZeGroupCount { public uint groupCountX; public uint groupCountY; public uint groupCountZ; }

/// <summary>Level Zero device properties and hardware topology descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZeDeviceProperties
{
    public int stype;
    public void* pNext;
    public int type;
    public uint vendorId;
    public uint deviceId;
    public uint flags;
    public uint subdeviceId;
    public uint coreClockRate;
    public ulong maxMemAllocSize;
    public uint maxHardwareContexts;
    public uint maxCommandQueuePriority;
    public uint numThreadsPerEU;
    public uint physicalEUSimdWidth;
    public uint numEUsPerSubslice;
    public uint numSubslicesPerSlice;
    public uint numSlices;
    public ulong timerResolution;
    public uint timestampValidBits;
    public uint kernelTimestampValidBits;
    public fixed byte uuid[16];
    public fixed byte name[256];
}