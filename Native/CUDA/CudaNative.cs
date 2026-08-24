using System.Runtime.InteropServices;

namespace GpuT.Agent.Native.CUDA;

public static unsafe partial class CudaNative
{
    private const string LibName = "libcuda.so.1";

    public const int CUDA_SUCCESS = 0;

    [LibraryImport(LibName, EntryPoint = "cuInit")]
    public static partial int cuInit(uint flags);

    [LibraryImport(LibName, EntryPoint = "cuDeviceGetCount")]
    public static partial int cuDeviceGetCount(int* count);

    [LibraryImport(LibName, EntryPoint = "cuDeviceGet")]
    public static partial int cuDeviceGet(int* device, int ordinal);

    [LibraryImport(LibName, EntryPoint = "cuDeviceGetName")]
    public static partial int cuDeviceGetName(byte* name, int len, int dev);

    [LibraryImport(LibName, EntryPoint = "cuCtxCreate_v2")]
    public static partial int cuCtxCreate(nint* pctx, uint flags, int dev);

    [LibraryImport(LibName, EntryPoint = "cuCtxDestroy_v2")]
    public static partial int cuCtxDestroy(nint ctx);

    [LibraryImport(LibName, EntryPoint = "cuMemAlloc_v2")]
    public static partial int cuMemAlloc(nint* dptr, nuint bytesize);

    [LibraryImport(LibName, EntryPoint = "cuMemFree_v2")]
    public static partial int cuMemFree(nint dptr);

    [LibraryImport(LibName, EntryPoint = "cuModuleLoadData")]
    public static partial int cuModuleLoadData(nint* module, void* image);

    [LibraryImport(LibName, EntryPoint = "cuModuleUnload")]
    public static partial int cuModuleUnload(nint hmod);

    [LibraryImport(LibName, EntryPoint = "cuModuleGetFunction")]
    public static partial int cuModuleGetFunction(nint* hfunc, nint hmod, byte* name);

    [LibraryImport(LibName, EntryPoint = "cuLaunchKernel")]
    public static partial int cuLaunchKernel(
        nint f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        nint hStream,
        void** kernelParams,
        void** extra);

    [LibraryImport(LibName, EntryPoint = "cuCtxSynchronize")]
    public static partial int cuCtxSynchronize();
}