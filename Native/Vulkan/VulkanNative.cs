using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Specification-defined StructureType constants and feature descriptors for Vulkan 1.0+ compute and ray tracing extensions.
/// </summary>
public static class VulkanConstants
{
    // Instance & Physical Device Properties 2
    public const StructureType StructureTypePhysicalDeviceFeatures2KHR = (StructureType)1000059000;
    public const StructureType StructureTypePhysicalDeviceProperties2KHR = (StructureType)1000059001;

    // Buffer Device Address
    public const StructureType StructureTypePhysicalDeviceBufferDeviceAddressFeaturesKHR = (StructureType)1000257000;
    public const StructureType StructureTypeBufferDeviceAddressInfo = (StructureType)1000244001;

    // Acceleration Structure
    public const StructureType StructureTypePhysicalDeviceAccelerationStructureFeaturesKHR = (StructureType)1000150013;
    public const StructureType StructureTypeAccelerationStructureCreateInfoKHR = (StructureType)1000150017;
    public const StructureType StructureTypeAccelerationStructureBuildGeometryInfoKHR = (StructureType)1000150000;
    public const StructureType StructureTypeAccelerationStructureGeometryKHR = (StructureType)1000150006;
    public const StructureType StructureTypeAccelerationStructureGeometryTrianglesDataKHR = (StructureType)1000150007;
    public const StructureType StructureTypeAccelerationStructureGeometryInstancesDataKHR = (StructureType)1000150008;
    public const StructureType StructureTypeAccelerationStructureBuildSizesInfoKHR = (StructureType)1000150020;
    public const StructureType StructureTypeAccelerationStructureDeviceAddressInfoKHR = (StructureType)1000150002;
    public const StructureType StructureTypeWriteDescriptorSetAccelerationStructureKHR = (StructureType)1000150007;

    // Ray Tracing & Ray Query
    public const StructureType StructureTypePhysicalDeviceRayQueryFeaturesKHR = (StructureType)1000348013;
    public const StructureType StructureTypePhysicalDeviceRayTracingPipelineFeaturesKHR = (StructureType)1000347000;
    public const StructureType StructureTypePhysicalDeviceRayTracingInvocationReorderFeaturesNV = (StructureType)1000490000;
    public const StructureType StructureTypeOpacityMicromapFeaturesEXT = (StructureType)1000396000;
    public const StructureType StructureTypePhysicalDeviceRayTracingPositionFetchFeaturesKHR = (StructureType)1000481000;

    // 16-Bit Storage & 8-Bit Storage
    public const StructureType StructureTypePhysicalDevice16BitStorageFeaturesKHR = (StructureType)1000083000;
    public const StructureType StructureTypePhysicalDevice8BitStorageFeaturesKHR = (StructureType)1000177000;

    // BFloat16 (VK_KHR_shader_bfloat16)
    public const StructureType StructureTypePhysicalDeviceShaderBfloat16FeaturesKHR = (StructureType)1000141000;

    // Vulkan Memory Model (VK_KHR_vulkan_memory_model)
    public const StructureType StructureTypePhysicalDeviceVulkanMemoryModelFeaturesKHR = (StructureType)1000211000;

    // Subgroup Size Control (VK_EXT_subgroup_size_control)
    public const StructureType StructureTypePhysicalDeviceSubgroupSizeControlFeaturesEXT = (StructureType)1000225002;

    // Performance Query (VK_KHR_performance_query)
    public const StructureType StructureTypePhysicalDevicePerformanceQueryFeaturesKHR = (StructureType)1000116000;
    public const StructureType StructureTypeQueryPoolPerformanceCreateInfoKHR = (StructureType)1000116002;
    public const StructureType StructureTypePerformanceQuerySubmitInfoKHR = (StructureType)1000116003;
    public const StructureType StructureTypeAcquireProfilingLockInfoKHR = (StructureType)1000116004;
    public const StructureType StructureTypePerformanceCounterKHR = (StructureType)1000116005;
    public const StructureType StructureTypePerformanceCounterDescriptionKHR = (StructureType)1000116006;

    // Cooperative Matrix
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesKHR = (StructureType)1000506000;
    public const StructureType StructureTypeCooperativeMatrixPropertiesKHR = (StructureType)1000506001;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixPropertiesKHR = (StructureType)1000506002;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrixFeaturesNV = (StructureType)1000249000;
    public const StructureType StructureTypePhysicalDeviceCooperativeMatrix2FeaturesNV = (StructureType)1000491000;

    // Integer Dot Product & Float16/Int8
    public const StructureType StructureTypePhysicalDeviceShaderIntegerDotProductFeaturesKHR = (StructureType)1000280000;
    public const StructureType StructureTypePhysicalDeviceShaderFloat16Int8FeaturesKHR = (StructureType)1000082000;

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
public unsafe struct VkPhysicalDeviceBufferDeviceAddressFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint bufferDeviceAddress;
    public uint bufferDeviceAddressCaptureReplay;
    public uint bufferDeviceAddressMultiDevice;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 40)]
public unsafe struct VkPhysicalDeviceAccelerationStructureFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint accelerationStructure;
    public uint accelerationStructureCaptureReplay;
    public uint accelerationStructureIndirectBuild;
    public uint accelerationStructureHostCommands;
    public uint descriptorBindingAccelerationStructureUpdateAfterBind;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceRayQueryFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint rayQuery;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 40)]
public unsafe struct VkPhysicalDeviceRayTracingPipelineFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint rayTracingPipeline;
    public uint rayTracingPipelineShaderGroupHandleCaptureReplay;
    public uint rayTracingPipelineShaderGroupHandleCaptureReplayMixed;
    public uint rayTracingPipelineTraceRaysIndirect;
    public uint rayTraversalPrimitiveCulling;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceRayTracingPositionFetchFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint rayTracingPositionFetch;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
public unsafe struct VkPhysicalDeviceRayTracingInvocationReorderFeaturesNV
{
    public StructureType sType;
    public void* pNext;
    public uint rayTracingInvocationReorder;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public unsafe struct VkPhysicalDeviceOpacityMicromapFeaturesEXT
{
    public StructureType sType;
    public void* pNext;
    public uint micromap;
    public uint micromapCaptureReplay;
    public uint micromapHostCommands;
    private uint _pad;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public unsafe struct VkPhysicalDevice16BitStorageFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint storageBuffer16BitAccess;
    public uint uniformAndStorageBuffer16BitAccess;
    public uint storagePushConstant16;
    public uint storageInputOutput16;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public unsafe struct VkPhysicalDevice8BitStorageFeaturesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint storageBuffer8BitAccess;
    public uint uniformAndStorageBuffer8BitAccess;
    public uint storagePushConstant8;
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

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkBufferDeviceAddressInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public Silk.NET.Vulkan.Buffer buffer;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAccelerationStructureCreateInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint createFlags;
    public Silk.NET.Vulkan.Buffer buffer;
    public ulong offset;
    public ulong size;
    public uint type;
    public ulong deviceAddress;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAccelerationStructureDeviceAddressInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public AccelerationStructureKHR accelerationStructure;
}

[StructLayout(LayoutKind.Explicit)]
public struct VkDeviceOrHostAddressConstKHR
{
    [FieldOffset(0)] public ulong deviceAddress;
    [FieldOffset(0)] public unsafe void* hostAddress;
}

[StructLayout(LayoutKind.Explicit)]
public struct VkDeviceOrHostAddressKHR
{
    [FieldOffset(0)] public ulong deviceAddress;
    [FieldOffset(0)] public unsafe void* hostAddress;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkAccelerationStructureGeometryTrianglesDataKHR
{
    public StructureType sType;
    public unsafe void* pNext;
    public uint vertexFormat;
    public VkDeviceOrHostAddressConstKHR vertexData;
    public ulong vertexStride;
    public uint maxVertex;
    public uint indexType;
    public VkDeviceOrHostAddressConstKHR indexData;
    public VkDeviceOrHostAddressConstKHR transformData;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkAccelerationStructureGeometryInstancesDataKHR
{
    public StructureType sType;
    public unsafe void* pNext;
    public uint arrayOfPointers;
    public VkDeviceOrHostAddressConstKHR data;
}

[StructLayout(LayoutKind.Explicit)]
public struct VkAccelerationStructureGeometryDataKHR
{
    [FieldOffset(0)] public VkAccelerationStructureGeometryTrianglesDataKHR triangles;
    [FieldOffset(0)] public VkAccelerationStructureGeometryInstancesDataKHR instances;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkAccelerationStructureGeometryKHR
{
    public StructureType sType;
    public unsafe void* pNext;
    public uint geometryType;
    public VkAccelerationStructureGeometryDataKHR geometry;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAccelerationStructureBuildGeometryInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint type;
    public uint flags;
    public uint mode;
    public AccelerationStructureKHR srcAccelerationStructure;
    public AccelerationStructureKHR dstAccelerationStructure;
    public uint geometryCount;
    public VkAccelerationStructureGeometryKHR* pGeometries;
    public VkAccelerationStructureGeometryKHR** ppGeometries;
    public VkDeviceOrHostAddressKHR scratchData;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkAccelerationStructureBuildSizesInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public ulong accelerationStructureSize;
    public ulong updateScratchSize;
    public ulong buildScratchSize;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkAccelerationStructureBuildRangeInfoKHR
{
    public uint primitiveCount;
    public uint primitiveOffset;
    public uint firstVertex;
    public uint transformOffset;
}

/// <summary>
/// 3x4 row-major transform matrix representing 12 contiguous floats (48 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VkTransformMatrixKHR
{
    public float M00, M01, M02, M03;
    public float M10, M11, M12, M13;
    public float M20, M21, M22, M23;

    public static VkTransformMatrixKHR Identity => new()
    {
        M00 = 1.0f, M01 = 0.0f, M02 = 0.0f, M03 = 0.0f,
        M10 = 0.0f, M11 = 1.0f, M12 = 0.0f, M13 = 0.0f,
        M20 = 0.0f, M21 = 0.0f, M22 = 1.0f, M23 = 0.0f
    };
}

/// <summary>
/// Exactly 64 bytes binary instance descriptor for Vulkan TLAS construction.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VkAccelerationStructureInstanceKHR
{
    public VkTransformMatrixKHR transform;
    public uint instanceCustomIndexAndMask;
    public uint instanceSbtAndFlags;
    public ulong accelerationStructureReference;

    public uint instanceCustomIndex
    {
        get => instanceCustomIndexAndMask & 0x00FFFFFF;
        set => instanceCustomIndexAndMask = (instanceCustomIndexAndMask & 0xFF000000) | (value & 0x00FFFFFF);
    }

    public uint mask
    {
        get => instanceCustomIndexAndMask >> 24;
        set => instanceCustomIndexAndMask = (instanceCustomIndexAndMask & 0x00FFFFFF) | ((value & 0xFF) << 24);
    }

    public uint instanceShaderBindingTableRecordOffset
    {
        get => instanceSbtAndFlags & 0x00FFFFFF;
        set => instanceSbtAndFlags = (instanceSbtAndFlags & 0xFF000000) | (value & 0x00FFFFFF);
    }

    public uint flags
    {
        get => instanceSbtAndFlags >> 24;
        set => instanceSbtAndFlags = (instanceSbtAndFlags & 0x00FFFFFF) | ((value & 0xFF) << 24);
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkWriteDescriptorSetAccelerationStructureKHR
{
    public StructureType sType;
    public void* pNext;
    public uint accelerationStructureCount;
    public AccelerationStructureKHR* pAccelerationStructures;
}