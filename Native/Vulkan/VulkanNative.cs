using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Native Vulkan loader bindings and interop structures strictly targeting the Linux platform (libvulkan.so.1).
/// </summary>
public static unsafe partial class VulkanNative
{
    /// <summary>
    /// Linux native Vulkan runtime library soname.
    /// </summary>
    public const string VulkanLib = "libvulkan.so.1";

    [LibraryImport(VulkanLib, EntryPoint = "vkGetInstanceProcAddr")]
    public static partial nint vkGetInstanceProcAddr(nint instance, byte* pName);

    /// <summary>
    /// Function pointer delegate for vkGetPhysicalDeviceQueueFamilyPerformanceQueryPassesKHR.
    /// </summary>
    public delegate void PFN_vkGetPhysicalDeviceQueueFamilyPerformanceQueryPassesKHR(
        nint physicalDevice,
        nint pPerformanceQueryInfo,
        uint* pNumPasses);

    /// <summary>
    /// Function pointer delegate for vkAcquireProfilingLockKHR.
    /// </summary>
    public delegate int PFN_vkAcquireProfilingLockKHR(nint device, VkAcquireProfilingLockInfoKHR* pInfo);

    /// <summary>
    /// Function pointer delegate for vkReleaseProfilingLockKHR.
    /// </summary>
    public delegate void PFN_vkReleaseProfilingLockKHR(nint device);
}

/// <summary>
/// Physical device features descriptor for VK_KHR_performance_query.
/// Enables counter query pool allocation on logical devices.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDevicePerformanceQueryFeaturesKHR
{
    public int sType;
    public void* pNext;
    public uint performanceCounterQueryPools;
    public uint performanceCounterMultipleQueryPools;
}

/// <summary>
/// Profiling lock acquisition descriptor for VK_KHR_performance_query.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAcquireProfilingLockInfoKHR
{
    public int sType;
    public void* pNext;
    public uint flags;
    public ulong timeout;
}

/// <summary>
/// Query parameters descriptor used to determine the number of required profiling passes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkQueryPoolPerformanceCreateInfoKHR
{
    public int sType;
    public void* pNext;
    public uint queueFamilyIndex;
    public uint counterIndexCount;
    public uint* pCounterIndices;
}