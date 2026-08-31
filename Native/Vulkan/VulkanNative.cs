using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Specification-defined StructureType constants for Vulkan 1.0 extensions.
/// </summary>
public static class VulkanConstants
{
    // Instance & Physical Device Properties 2
    public const StructureType StructureTypePhysicalDeviceFeatures2KHR = (StructureType)1000059000;
    public const StructureType StructureTypePhysicalDeviceProperties2KHR = (StructureType)1000059001;

    // Performance Query (VK_KHR_performance_query)
    public const StructureType StructureTypePhysicalDevicePerformanceQueryFeaturesKHR = (StructureType)1000116000;
    public const StructureType StructureTypeQueryPoolPerformanceCreateInfoKHR = (StructureType)1000116002;
    public const StructureType StructureTypePerformanceQuerySubmitInfoKHR = (StructureType)1000116003;
    public const StructureType StructureTypeAcquireProfilingLockInfoKHR = (StructureType)1000116004;
    public const StructureType StructureTypePerformanceCounterKHR = (StructureType)1000116005;
    public const StructureType StructureTypePerformanceCounterDescriptionKHR = (StructureType)1000116006;

    // Cooperative Matrix (VK_KHR_cooperative_matrix & VK_NV_cooperative_matrix2)
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesKHR = (StructureType)1000506000;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesNV = (StructureType)1000249000;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrix2FeaturesNV = (StructureType)1000491000;

    // Integer Dot Product (VK_KHR_shader_integer_dot_product)
    public const StructureType StructureTypePhysicalDeviceShaderIntegerDotProductFeaturesKHR = (StructureType)1000280000;

    // Float16 & Int8 (VK_KHR_shader_float16_int8)
    public const StructureType StructureTypePhysicalDeviceShaderFloat16Int8FeaturesKHR = (StructureType)1000082000;

    // Ray Tracing & Ray Query (VK_KHR_ray_query, VK_KHR_ray_tracing_pipeline, VK_KHR_opacity_micromap)
    public const StructureType StructureTypePhysicalDeviceRayQueryFeaturesKHR = (StructureType)1000348013;
    public const StructureType StructureTypePhysicalDeviceRayTracingPipelineFeaturesKHR = (StructureType)1000347000;
    public const StructureType StructureTypePhysicalDeviceRayTracingInvocationReorderFeaturesNV = (StructureType)1000490000;
    public const StructureType StructureTypePhysicalDeviceOpacityMicromapFeaturesEXT = (StructureType)1000396000;
    public const StructureType StructureTypePhysicalDeviceRayTracingPositionFetchFeaturesKHR = (StructureType)1000481000;

    // Float8 (VK_EXT_shader_float8 / VK_KHR_shader_float8)
    public const StructureType StructureTypePhysicalDeviceShaderFloat8FeaturesEXT = (StructureType)1000600000;
}

/// <summary>
/// Native Vulkan debug utils messenger descriptor compliant with C-ABI and .NET NativeAOT.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsMessengerCreateInfoEXT
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public DebugUtilsMessageSeverityFlagsEXT messageSeverity;
    public DebugUtilsMessageTypeFlagsEXT messageType;
    public delegate* unmanaged[Cdecl]<DebugUtilsMessageSeverityFlagsEXT, DebugUtilsMessageTypeFlagsEXT, DebugUtilsMessengerCallbackDataEXT*, void*, uint> pfnUserCallback;
    public void* pUserData;
}

/// <summary>
/// Execution pass descriptor for submitting performance queries to a Vulkan queue.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPerformanceQuerySubmitInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint counterPassIndex;
}