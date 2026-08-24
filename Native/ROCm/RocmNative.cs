using System.Runtime.InteropServices;

namespace GpuT.Agent.Native.ROCm;

public static unsafe partial class RocmNative
{
    private const string HipLib = "libamdhip64.so";
    private const string RtcLib = "libhiprtc.so";

    public const int HIP_SUCCESS = 0;
    public const int HIPRTC_SUCCESS = 0;

    [LibraryImport(HipLib, EntryPoint = "hipInit")]
    public static partial int hipInit(uint flags);

    [LibraryImport(HipLib, EntryPoint = "hipGetDeviceCount")]
    public static partial int hipGetDeviceCount(int* count);

    [LibraryImport(HipLib, EntryPoint = "hipGetDevice")]
    public static partial int hipGetDevice(int* deviceId);

    [LibraryImport(HipLib, EntryPoint = "hipSetDevice")]
    public static partial int hipSetDevice(int deviceId);

    [LibraryImport(HipLib, EntryPoint = "hipDeviceGetName")]
    public static partial int hipDeviceGetName(byte* name, int len, int deviceId);

    [LibraryImport(HipLib, EntryPoint = "hipDeviceReset")]
    public static partial int hipDeviceReset(); // Сброс очередей MES в Idle

    [LibraryImport(HipLib, EntryPoint = "hipMalloc")]
    public static partial int hipMalloc(nint* ptr, nuint size);

    [LibraryImport(HipLib, EntryPoint = "hipFree")]
    public static partial int hipFree(nint ptr);

    [LibraryImport(HipLib, EntryPoint = "hipModuleLoadData")]
    public static partial int hipModuleLoadData(nint* module, void* image);

    [LibraryImport(HipLib, EntryPoint = "hipModuleUnload")]
    public static partial int hipModuleUnload(nint module);

    [LibraryImport(HipLib, EntryPoint = "hipModuleGetFunction")]
    public static partial int hipModuleGetFunction(nint* function, nint module, byte* kname);

    [LibraryImport(HipLib, EntryPoint = "hipModuleLaunchKernel")]
    public static partial int hipModuleLaunchKernel(
        nint f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        nint stream,
        void** kernelParams,
        void** extra);

    [LibraryImport(HipLib, EntryPoint = "hipDeviceSynchronize")]
    public static partial int hipDeviceSynchronize();

    [LibraryImport(RtcLib, EntryPoint = "hiprtcCreateProgram")]
    public static partial int hiprtcCreateProgram(nint* prog, byte* src, byte* name, int numHeaders, byte** headers, byte** includeNames);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcDestroyProgram")]
    public static partial int hiprtcDestroyProgram(nint* prog);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcCompileProgram")]
    public static partial int hiprtcCompileProgram(nint prog, int numOptions, byte** options);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcGetCodeSize")]
    public static partial int hiprtcGetCodeSize(nint prog, nuint* codeSize);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcGetCode")]
    public static partial int hiprtcGetCode(nint prog, byte* code);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcGetProgramLogSize")]
    public static partial int hiprtcGetProgramLogSize(nint prog, nuint* logSize);

    [LibraryImport(RtcLib, EntryPoint = "hiprtcGetProgramLog")]
    public static partial int hiprtcGetProgramLog(nint prog, byte* log);
}