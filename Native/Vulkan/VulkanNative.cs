using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Direct P/Invoke bindings for Vulkan instance and device management (libvulkan.so.1).
/// </summary>
public static unsafe partial class VulkanNative
{
    private const string VulkanLib = "libvulkan.so.1";

    [LibraryImport(VulkanLib, EntryPoint = "vkGetInstanceProcAddr")]
    public static partial nint vkGetInstanceProcAddr(nint instance, byte* pName);

    [LibraryImport(VulkanLib, EntryPoint = "vkCreateInstance")]
    public static partial int vkCreateInstance(VkInstanceCreateInfo* pCreateInfo, nint pAllocator, nint* pInstance);

    [LibraryImport(VulkanLib, EntryPoint = "vkDestroyInstance")]
    public static partial void vkDestroyInstance(nint instance, nint pAllocator);

    [LibraryImport(VulkanLib, EntryPoint = "vkEnumeratePhysicalDevices")]
    public static partial int vkEnumeratePhysicalDevices(nint instance, uint* pPhysicalDeviceCount, nint* pPhysicalDevices);

    [LibraryImport(VulkanLib, EntryPoint = "vkCreateDevice")]
    public static partial int vkCreateDevice(nint physicalDevice, VkDeviceCreateInfo* pCreateInfo, nint pAllocator, nint* pDevice);

    [LibraryImport(VulkanLib, EntryPoint = "vkDestroyDevice")]
    public static partial void vkDestroyDevice(nint device, nint pAllocator);

    // Extension delegates for VK_KHR_performance_query
    public delegate int PFN_vkAcquireProfilingLockKHR(nint device, VkAcquireProfilingLockInfoKHR* pInfo);
    public delegate void PFN_vkReleaseProfilingLockKHR(nint device);
}

/// <summary>Vulkan instance creation descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkInstanceCreateInfo
{
    public int sType;
    public void* pNext;
    public uint flags;
    public void* pApplicationInfo;
    public uint enabledLayerCount;
    public byte** ppEnabledLayerNames;
    public uint enabledExtensionCount;
    public byte** ppEnabledExtensionNames;
}

/// <summary>Vulkan logical device creation descriptor.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDeviceCreateInfo
{
    public int sType;
    public void* pNext;
    public uint flags;
    public uint queueCreateInfoCount;
    public void* pQueueCreateInfos;
    public uint enabledLayerCount;
    public byte** ppEnabledLayerNames;
    public uint enabledExtensionCount;
    public byte** ppEnabledExtensionNames;
    public void* pEnabledFeatures;
}

/// <summary>Profiling lock parameters for VK_KHR_performance_query.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAcquireProfilingLockInfoKHR
{
    public int sType;
    public void* pNext;
    public uint flags;
    public ulong timeout;
}