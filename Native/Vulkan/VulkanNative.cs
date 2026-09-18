using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Specification-defined StructureType constants and feature descriptors for Vulkan 1.0+ compute extensions.
/// </summary>
public static class VulkanConstants
{
    // Instance & Physical Device Properties 2 (Vulkan 1.0 KHR)
    public const StructureType StructureTypePhysicalDeviceFeatures2KHR = (StructureType)1000059000;
    public const StructureType StructureTypePhysicalDeviceProperties2KHR = (StructureType)1000059001;

    // BFloat16 (VK_KHR_shader_bfloat16)
    public const StructureType StructureTypePhysicalDeviceShaderBfloat16FeaturesKHR = (StructureType)1000141000;

    // Vulkan Memory Model (VK_KHR_vulkan_memory_model)
    public const StructureType StructureTypePhysicalDeviceVulkanMemoryModelFeaturesKHR = (StructureType)1000211000;

    // Subgroup Size Control (VK_EXT_subgroup_size_control)
    public const StructureType StructureTypePhysicalDeviceSubgroupSizeControlFeaturesEXT = (StructureType)1000225000;

    // Performance Query (VK_KHR_performance_query)
    public const StructureType StructureTypePhysicalDevicePerformanceQueryFeaturesKHR = (StructureType)1000116000;
    public const StructureType StructureTypeQueryPoolPerformanceCreateInfoKHR = (StructureType)1000116002;
    public const StructureType StructureTypePerformanceQuerySubmitInfoKHR = (StructureType)1000116003;
    public const StructureType StructureTypeAcquireProfilingLockInfoKHR = (StructureType)1000116004;
    public const StructureType StructureTypePerformanceCounterKHR = (StructureType)1000116005;
    public const StructureType StructureTypePerformanceCounterDescriptionKHR = (StructureType)1000116006;

    // Cooperative Matrix (VK_KHR_cooperative_matrix & VK_NV_cooperative_matrix)
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesKHR = (StructureType)1000506000;
    public const StructureType StructureTypeCooperativeMatrixPropertiesKHR = (StructureType)1000506001;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixPropertiesKHR = (StructureType)1000506002;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesNV = (StructureType)1000249000;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrix2FeaturesNV = (StructureType)1000491000;

    // Integer Dot Product (VK_KHR_shader_integer_dot_product)
    public const StructureType StructureTypePhysicalDeviceShaderIntegerDotProductFeaturesKHR = (StructureType)1000280000;

    // Float16 & Int8 (VK_KHR_shader_float16_int8)
    public const StructureType StructureTypePhysicalDeviceShaderFloat16Int8FeaturesKHR = (StructureType)1000082000;

    // Ray Tracing & Ray Query
    public const StructureType StructureTypePhysicalDeviceRayQueryFeaturesKHR = (StructureType)1000348013;
    public const StructureType StructureTypePhysicalDeviceRayTracingPipelineFeaturesKHR = (StructureType)1000347000;
    public const StructureType StructureTypePhysicalDeviceRayTracingInvocationReorderFeaturesNV = (StructureType)1000490000;
    public const StructureType StructureTypePhysicalDeviceOpacityMicromapFeaturesEXT = (StructureType)1000396000;
    public const StructureType StructureTypePhysicalDeviceRayTracingPositionFetchFeaturesKHR = (StructureType)1000481000;

    // Float8 (VK_EXT_shader_float8)
    public const StructureType StructureTypePhysicalDeviceShaderFloat8FeaturesEXT = (StructureType)1000600000;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 240)]
public unsafe struct VkPhysicalDeviceFeatures2KHR
{
    public StructureType sType;
    public void* pNext;
    public PhysicalDeviceFeatures features;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public unsafe struct VkPhysicalDeviceVulkanMemoryModelFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint vulkanMemoryModel;
    public uint vulkanMemoryModelDeviceScope;
    public uint vulkanMemoryModelAvailabilityVisibilityChains;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceSubgroupSizeControlFeaturesEXT
{
    public StructureType sType;
    public void* pNext;
    public uint subgroupSizeControl;
    public uint computeFullSubgroups;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public unsafe struct VkPhysicalDeviceShaderBfloat16FeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint shaderBFloat16Type;
    public uint shaderBFloat16DotProduct;
    public uint shaderBFloat16CooperativeMatrix;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceCooperativeMatrixFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint cooperativeMatrix;
    public uint cooperativeMatrixRobustBufferAccess;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceShaderIntegerDotProductFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint shaderIntegerDotProduct;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceShaderFloat16Int8FeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint shaderFloat16;
    public uint shaderInt8;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 56)]
public unsafe struct VkCooperativeMatrixPropertiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint MSize;
    public uint NSize;
    public uint KSize;
    public ComponentTypeKHR AType;
    public ComponentTypeKHR BType;
    public ComponentTypeKHR CType;
    public ComponentTypeKHR ResultType;
    public uint saturatingAccumulation;
    public ScopeKHR scope;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
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

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPerformanceQuerySubmitInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint counterPassIndex;
    private uint _pad;
}