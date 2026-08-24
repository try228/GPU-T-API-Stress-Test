using System.Runtime.InteropServices;

namespace GpuT.Agent.Native.OpenCL;

public static unsafe partial class OpenClNative
{
    private const string LibName = "libOpenCL.so.1";

    public const uint CL_DEVICE_TYPE_GPU = 1 << 2;
    public const uint CL_DEVICE_TYPE_ALL = 0xFFFFFFFF;

    public const uint CL_PLATFORM_NAME = 0x0902;
    public const uint CL_PLATFORM_VENDOR = 0x0903;
    public const uint CL_DEVICE_NAME = 0x102B;
    public const uint CL_DEVICE_VENDOR = 0x102C;
    public const uint CL_DRIVER_VERSION = 0x102D;

    public const ulong CL_MEM_READ_WRITE = 1 << 0;

    public const uint CL_PROGRAM_BUILD_LOG = 0x1183;
    public const int CL_SUCCESS = 0;

    [LibraryImport(LibName, EntryPoint = "clGetPlatformIDs")]
    public static partial int clGetPlatformIDs(uint num_entries, nint* platforms, uint* num_platforms);

    [LibraryImport(LibName, EntryPoint = "clGetPlatformInfo")]
    public static partial int clGetPlatformInfo(nint platform, uint param_name, nuint param_value_size, void* param_value, nuint* param_value_size_ret);

    [LibraryImport(LibName, EntryPoint = "clGetDeviceIDs")]
    public static partial int clGetDeviceIDs(nint platform, uint device_type, uint num_entries, nint* devices, uint* num_devices);

    [LibraryImport(LibName, EntryPoint = "clGetDeviceInfo")]
    public static partial int clGetDeviceInfo(nint device, uint param_name, nuint param_value_size, void* param_value, nuint* param_value_size_ret);

    [LibraryImport(LibName, EntryPoint = "clCreateContext")]
    public static partial nint clCreateContext(nint* properties, uint num_devices, nint* devices, void* pfn_notify, void* user_data, int* errcode_ret);

    [LibraryImport(LibName, EntryPoint = "clCreateCommandQueue")]
    public static partial nint clCreateCommandQueue(nint context, nint device, ulong properties, int* errcode_ret);

    [LibraryImport(LibName, EntryPoint = "clCreateBuffer")]
    public static partial nint clCreateBuffer(nint context, ulong flags, nuint size, void* host_ptr, int* errcode_ret);

    [LibraryImport(LibName, EntryPoint = "clCreateProgramWithSource")]
    public static partial nint clCreateProgramWithSource(nint context, uint count, byte** strings, nuint* lengths, int* errcode_ret);

    [LibraryImport(LibName, EntryPoint = "clBuildProgram")]
    public static partial int clBuildProgram(nint program, uint num_devices, nint* device_list, byte* options, void* pfn_notify, void* user_data);

    [LibraryImport(LibName, EntryPoint = "clGetProgramBuildInfo")]
    public static partial int clGetProgramBuildInfo(nint program, nint device, uint param_name, nuint param_value_size, void* param_value, nuint* param_value_size_ret);

    [LibraryImport(LibName, EntryPoint = "clCreateKernel")]
    public static partial nint clCreateKernel(nint program, byte* kernel_name, int* errcode_ret);

    [LibraryImport(LibName, EntryPoint = "clSetKernelArg")]
    public static partial int clSetKernelArg(nint kernel, uint arg_index, nuint arg_size, void* arg_value);

    [LibraryImport(LibName, EntryPoint = "clEnqueueNDRangeKernel")]
    public static partial int clEnqueueNDRangeKernel(nint command_queue, nint kernel, uint work_dim, nuint* global_work_offset, nuint* global_work_size, nuint* local_work_size, uint num_events_in_wait_list, nint* event_wait_list, nint* @event);

    [LibraryImport(LibName, EntryPoint = "clFlush")]
    public static partial int clFlush(nint command_queue);

    [LibraryImport(LibName, EntryPoint = "clFinish")]
    public static partial int clFinish(nint command_queue);

    [LibraryImport(LibName, EntryPoint = "clReleaseKernel")]
    public static partial int clReleaseKernel(nint kernel);

    [LibraryImport(LibName, EntryPoint = "clReleaseProgram")]
    public static partial int clReleaseProgram(nint program);

    [LibraryImport(LibName, EntryPoint = "clReleaseMemObject")]
    public static partial int clReleaseMemObject(nint memobj);

    [LibraryImport(LibName, EntryPoint = "clReleaseCommandQueue")]
    public static partial int clReleaseCommandQueue(nint command_queue);

    [LibraryImport(LibName, EntryPoint = "clReleaseContext")]
    public static partial int clReleaseContext(nint context);
}