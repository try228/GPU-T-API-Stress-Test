using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Allocates device-addressable buffers, builds BLAS and TLAS hierarchies,
/// and manages hardware Acceleration Structures for VK_KHR_ray_query stress testing.
/// </summary>
public sealed unsafe class VulkanRtManager : IDisposable
{
    private delegate* unmanaged[Cdecl]<Device, VkBufferDeviceAddressInfoKHR*, ulong> _pfnGetBufferDeviceAddress;
    private delegate* unmanaged[Cdecl]<Device, VkAccelerationStructureCreateInfoKHR*, AllocationCallbacks*, AccelerationStructureKHR*, Result> _pfnCreateAccelerationStructure;
    private delegate* unmanaged[Cdecl]<Device, AccelerationStructureKHR, AllocationCallbacks*, void> _pfnDestroyAccelerationStructure;
    private delegate* unmanaged[Cdecl]<Device, uint, VkAccelerationStructureBuildGeometryInfoKHR*, uint*, VkAccelerationStructureBuildSizesInfoKHR*, void> _pfnGetBuildSizes;
    private delegate* unmanaged[Cdecl]<CommandBuffer, uint, VkAccelerationStructureBuildGeometryInfoKHR*, VkAccelerationStructureBuildRangeInfoKHR**, void> _pfnCmdBuildAccelerationStructures;
    private delegate* unmanaged[Cdecl]<Device, VkAccelerationStructureDeviceAddressInfoKHR*, ulong> _pfnGetAccelerationStructureDeviceAddress;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Queue _queue;
    private readonly CommandPool _cmdPool;
    private readonly PhysicalDeviceMemoryProperties _memProps;

    private VkBuffer _vtxBuffer, _idxBuffer, _blasBuffer, _tlasBuffer, _instBuffer, _scratchBuffer;
    private DeviceMemory _vtxMem, _idxMem, _blasMem, _tlasMem, _instMem, _scratchMem;

    public AccelerationStructureKHR Blas { get; private set; }
    public AccelerationStructureKHR Tlas { get; private set; }
    public bool IsInitialized { get; private set; }

    public VulkanRtManager(
        Vk vk, Device device, PhysicalDevice physicalDevice,
        Queue queue, CommandPool cmdPool, PhysicalDeviceMemoryProperties memProps)
    {
        _vk = vk;
        _device = device;
        _physicalDevice = physicalDevice;
        _queue = queue;
        _cmdPool = cmdPool;
        _memProps = memProps;
    }

    /// <summary>
    /// Loads function pointers and constructs the complete BLAS and TLAS hierarchy on the GPU.
    /// </summary>
    public bool Initialize()
    {
        if (!LoadFunctionPointers()) return false;

        try
        {
            BuildMeshAndBLAS();
            BuildTLAS();
            IsInitialized = Tlas.Handle != 0;
            return IsInitialized;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[VulkanRtManager] Acceleration Structure initialization skipped: {ex.Message}");
            Console.ResetColor();
            Cleanup();
            return false;
        }
    }

    private bool LoadFunctionPointers()
    {
        nint pGetBda = GetDeviceProc("vkGetBufferDeviceAddressKHR");
        if (pGetBda == 0) pGetBda = GetDeviceProc("vkGetBufferDeviceAddress");
        nint pCreateAs = GetDeviceProc("vkCreateAccelerationStructureKHR");
        nint pDestroyAs = GetDeviceProc("vkDestroyAccelerationStructureKHR");
        nint pGetSizes = GetDeviceProc("vkGetAccelerationStructureBuildSizesKHR");
        nint pBuildAs = GetDeviceProc("vkCmdBuildAccelerationStructuresKHR");
        nint pGetAsDa = GetDeviceProc("vkGetAccelerationStructureDeviceAddressKHR");

        if (pGetBda == 0 || pCreateAs == 0 || pDestroyAs == 0 || pGetSizes == 0 || pBuildAs == 0 || pGetAsDa == 0)
            return false;

        _pfnGetBufferDeviceAddress = (delegate* unmanaged[Cdecl]<Device, VkBufferDeviceAddressInfoKHR*, ulong>)pGetBda;
        _pfnCreateAccelerationStructure = (delegate* unmanaged[Cdecl]<Device, VkAccelerationStructureCreateInfoKHR*, AllocationCallbacks*, AccelerationStructureKHR*, Result>)pCreateAs;
        _pfnDestroyAccelerationStructure = (delegate* unmanaged[Cdecl]<Device, AccelerationStructureKHR, AllocationCallbacks*, void>)pDestroyAs;
        _pfnGetBuildSizes = (delegate* unmanaged[Cdecl]<Device, uint, VkAccelerationStructureBuildGeometryInfoKHR*, uint*, VkAccelerationStructureBuildSizesInfoKHR*, void>)pGetSizes;
        _pfnCmdBuildAccelerationStructures = (delegate* unmanaged[Cdecl]<CommandBuffer, uint, VkAccelerationStructureBuildGeometryInfoKHR*, VkAccelerationStructureBuildRangeInfoKHR**, void>)pBuildAs;
        _pfnGetAccelerationStructureDeviceAddress = (delegate* unmanaged[Cdecl]<Device, VkAccelerationStructureDeviceAddressInfoKHR*, ulong>)pGetAsDa;

        return true;
    }

    private nint GetDeviceProc(string name)
    {
        nint pStr = SilkMarshal.StringToPtr(name);
        nint proc = _vk.GetDeviceProcAddr(_device, (byte*)pStr);
        SilkMarshal.Free(pStr);
        return proc;
    }

    private uint FindMemoryType(uint bits, MemoryPropertyFlags preferredFlags, MemoryPropertyFlags requiredFlags = 0)
    {
        for (uint i = 0; i < _memProps.MemoryTypeCount; i++)
        {
            if ((bits & (1u << (int)i)) != 0 &&
                (_memProps.MemoryTypes[(int)i].PropertyFlags & (preferredFlags | requiredFlags)) == (preferredFlags | requiredFlags))
            {
                return i;
            }
        }
        if (requiredFlags != 0)
        {
            for (uint i = 0; i < _memProps.MemoryTypeCount; i++)
            {
                if ((bits & (1u << (int)i)) != 0 &&
                    (_memProps.MemoryTypes[(int)i].PropertyFlags & requiredFlags) == requiredFlags)
                {
                    return i;
                }
            }
        }
        for (uint i = 0; i < _memProps.MemoryTypeCount; i++)
        {
            if ((bits & (1u << (int)i)) != 0)
                return i;
        }
        throw new InvalidOperationException("Failed to find suitable memory type for Acceleration Structure buffer.");
    }

    private void CreateDeviceAddressBuffer(
        ulong size,
        BufferUsageFlags usage,
        MemoryPropertyFlags preferredFlags,
        MemoryPropertyFlags requiredFlags,
        out VkBuffer buffer,
        out DeviceMemory memory,
        out ulong deviceAddress)
    {
        BufferCreateInfo bufInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage | BufferUsageFlags.ShaderDeviceAddressBit,
            SharingMode = SharingMode.Exclusive
        };

        Result bufRes = _vk.CreateBuffer(_device, &bufInfo, null, out buffer);
        if (bufRes != Result.Success)
            throw new InvalidOperationException($"vkCreateBuffer failed: {bufRes}");

        MemoryRequirements reqs;
        _vk.GetBufferMemoryRequirements(_device, buffer, &reqs);

        MemoryAllocateFlagsInfo flagsInfo = new()
        {
            SType = StructureType.MemoryAllocateFlagsInfo,
            Flags = MemoryAllocateFlags.AddressBit
        };

        uint memType = FindMemoryType(reqs.MemoryTypeBits, preferredFlags, requiredFlags);
        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            PNext = &flagsInfo,
            AllocationSize = reqs.Size,
            MemoryTypeIndex = memType
        };

        Result allocRes = _vk.AllocateMemory(_device, &allocInfo, null, out memory);
        if (allocRes != Result.Success)
            throw new InvalidOperationException($"vkAllocateMemory failed: {allocRes}");

        _vk.BindBufferMemory(_device, buffer, memory, 0);

        VkBufferDeviceAddressInfoKHR bdaInfo = new()
        {
            sType = VulkanConstants.StructureTypeBufferDeviceAddressInfo,
            pNext = null,
            buffer = buffer
        };
        deviceAddress = _pfnGetBufferDeviceAddress(_device, &bdaInfo);
        if (deviceAddress == 0)
            throw new InvalidOperationException("vkGetBufferDeviceAddress returned 0.");
    }

    private void BuildMeshAndBLAS()
    {
        // 1. Procedural 3D mesh: Multi-faceted geometric sphere stressing the Ray Accelerators
        float[] vertices = new float[]
        {
            -1.0f, -1.0f,  1.0f,    1.0f, -1.0f,  1.0f,    1.0f,  1.0f,  1.0f,   -1.0f,  1.0f,  1.0f,
            -1.0f, -1.0f, -1.0f,    1.0f, -1.0f, -1.0f,    1.0f,  1.0f, -1.0f,   -1.0f,  1.0f, -1.0f,
             0.0f,  2.0f,  0.0f,    0.0f, -2.0f,  0.0f
        };

        uint[] indices = new uint[]
        {
            0, 1, 2,  2, 3, 0,
            1, 5, 6,  6, 2, 1,
            5, 4, 7,  7, 6, 5,
            4, 0, 3,  3, 7, 4,
            3, 2, 8,  2, 6, 8,
            6, 7, 8,  7, 3, 8,
            0, 4, 9,  4, 5, 9,
            5, 1, 9,  1, 0, 9
        };

        ulong vtxSize = (ulong)(vertices.Length * sizeof(float));
        ulong idxSize = (ulong)(indices.Length * sizeof(uint));

        CreateDeviceAddressBuffer(
            vtxSize,
            BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            MemoryPropertyFlags.HostVisibleBit,
            out _vtxBuffer, out _vtxMem, out ulong vtxAddress);

        CreateDeviceAddressBuffer(
            idxSize,
            BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            MemoryPropertyFlags.HostVisibleBit,
            out _idxBuffer, out _idxMem, out ulong idxAddress);

        void* pData = null;
        Result mapRes = _vk.MapMemory(_device, _vtxMem, 0, vtxSize, 0, &pData);
        if (mapRes != Result.Success || pData == null)
            throw new InvalidOperationException($"vkMapMemory for vertices failed: {mapRes}");
        fixed (float* pSrc = vertices) Unsafe.CopyBlock(pData, pSrc, (uint)vtxSize);
        _vk.UnmapMemory(_device, _vtxMem);

        pData = null;
        mapRes = _vk.MapMemory(_device, _idxMem, 0, idxSize, 0, &pData);
        if (mapRes != Result.Success || pData == null)
            throw new InvalidOperationException($"vkMapMemory for indices failed: {mapRes}");
        fixed (uint* pSrc = indices) Unsafe.CopyBlock(pData, pSrc, (uint)idxSize);
        _vk.UnmapMemory(_device, _idxMem);

        // 2. Geometry configuration
        VkAccelerationStructureGeometryKHR geom = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureGeometryKHR,
            pNext = null,
            geometryType = 0, // Triangles (VK_GEOMETRY_TYPE_TRIANGLES_KHR)
            flags = 1 // Opaque (VK_GEOMETRY_OPAQUE_BIT_KHR)
        };

        geom.geometry.triangles = new VkAccelerationStructureGeometryTrianglesDataKHR()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureGeometryTrianglesDataKHR,
            pNext = null,
            vertexFormat = (uint)Format.R32G32B32Sfloat,
            vertexData = new VkDeviceOrHostAddressConstKHR { deviceAddress = vtxAddress },
            vertexStride = (ulong)(sizeof(float) * 3),
            maxVertex = (uint)(vertices.Length / 3 - 1),
            indexType = (uint)IndexType.Uint32,
            indexData = new VkDeviceOrHostAddressConstKHR { deviceAddress = idxAddress },
            transformData = default
        };

        uint primCount = (uint)(indices.Length / 3);

        VkAccelerationStructureBuildGeometryInfoKHR buildInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureBuildGeometryInfoKHR,
            pNext = null,
            type = 1, // BottomLevel (VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR)
            flags = 1, // PreferFastTrace (VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR)
            mode = 0,  // Build (VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR)
            geometryCount = 1,
            pGeometries = &geom,
            ppGeometries = null
        };

        VkAccelerationStructureBuildSizesInfoKHR sizeInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureBuildSizesInfoKHR,
            pNext = null
        };

        // Pass 1u (VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR) to prevent CPU memory dereferencing
        _pfnGetBuildSizes(_device, 1u, &buildInfo, &primCount, &sizeInfo);

        CreateDeviceAddressBuffer(
            sizeInfo.accelerationStructureSize,
            BufferUsageFlags.AccelerationStructureStorageBitKhr,
            MemoryPropertyFlags.DeviceLocalBit,
            0,
            out _blasBuffer, out _blasMem, out _);

        VkAccelerationStructureCreateInfoKHR createInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureCreateInfoKHR,
            pNext = null,
            createFlags = 0,
            buffer = _blasBuffer,
            offset = 0,
            size = sizeInfo.accelerationStructureSize,
            type = 1, // BottomLevel
            deviceAddress = 0
        };

        AccelerationStructureKHR blasHandle;
        Result asRes = _pfnCreateAccelerationStructure(_device, &createInfo, null, &blasHandle);
        if (asRes != Result.Success || blasHandle.Handle == 0)
            throw new InvalidOperationException($"vkCreateAccelerationStructureKHR failed for BLAS: {asRes}");
        Blas = blasHandle;

        CreateDeviceAddressBuffer(
            sizeInfo.buildScratchSize + 256,
            BufferUsageFlags.StorageBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            0,
            out _scratchBuffer, out _scratchMem, out ulong scratchAddress);

        ulong alignedScratch = (scratchAddress + 255UL) & ~255UL;
        buildInfo.dstAccelerationStructure = Blas;
        buildInfo.scratchData.deviceAddress = alignedScratch;

        VkAccelerationStructureBuildRangeInfoKHR range = new()
        {
            primitiveCount = primCount,
            primitiveOffset = 0,
            firstVertex = 0,
            transformOffset = 0
        };
        VkAccelerationStructureBuildRangeInfoKHR* pRange = &range;

        CommandBuffer cb = BeginImmediateCommands();
        _pfnCmdBuildAccelerationStructures(cb, 1, &buildInfo, &pRange);
        EndImmediateCommands(cb);
    }

    private void BuildTLAS()
    {
        VkAccelerationStructureDeviceAddressInfoKHR daInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureDeviceAddressInfoKHR,
            pNext = null,
            accelerationStructure = Blas
        };
        ulong blasDeviceAddress = _pfnGetAccelerationStructureDeviceAddress(_device, &daInfo);
        if (blasDeviceAddress == 0)
            throw new InvalidOperationException("vkGetAccelerationStructureDeviceAddressKHR returned 0 for BLAS.");

        VkAccelerationStructureInstanceKHR instance = new()
        {
            transform = VkTransformMatrixKHR.Identity,
            instanceCustomIndex = 0,
            mask = 0xFF,
            instanceShaderBindingTableRecordOffset = 0,
            flags = 0,
            accelerationStructureReference = blasDeviceAddress
        };

        ulong instSize = (ulong)sizeof(VkAccelerationStructureInstanceKHR);
        CreateDeviceAddressBuffer(
            instSize,
            BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            MemoryPropertyFlags.HostVisibleBit,
            out _instBuffer, out _instMem, out ulong instAddress);

        void* pData = null;
        Result mapRes = _vk.MapMemory(_device, _instMem, 0, instSize, 0, &pData);
        if (mapRes != Result.Success || pData == null)
            throw new InvalidOperationException($"vkMapMemory for instances failed: {mapRes}");
        Unsafe.CopyBlock(pData, &instance, (uint)instSize);
        _vk.UnmapMemory(_device, _instMem);

        VkAccelerationStructureGeometryKHR geom = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureGeometryKHR,
            pNext = null,
            geometryType = 2, // Instances (VK_GEOMETRY_TYPE_INSTANCES_KHR)
            flags = 1 // Opaque
        };

        geom.geometry.instances = new VkAccelerationStructureGeometryInstancesDataKHR()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureGeometryInstancesDataKHR,
            pNext = null,
            arrayOfPointers = 0,
            data = new VkDeviceOrHostAddressConstKHR { deviceAddress = instAddress }
        };

        uint instanceCount = 1;

        VkAccelerationStructureBuildGeometryInfoKHR buildInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureBuildGeometryInfoKHR,
            pNext = null,
            type = 0, // TopLevel (VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR)
            flags = 1, // PreferFastTrace
            mode = 0,  // Build
            geometryCount = 1,
            pGeometries = &geom,
            ppGeometries = null
        };

        VkAccelerationStructureBuildSizesInfoKHR sizeInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureBuildSizesInfoKHR,
            pNext = null
        };

        // Pass 1u (DEVICE build)
        _pfnGetBuildSizes(_device, 1u, &buildInfo, &instanceCount, &sizeInfo);

        CreateDeviceAddressBuffer(
            sizeInfo.accelerationStructureSize,
            BufferUsageFlags.AccelerationStructureStorageBitKhr,
            MemoryPropertyFlags.DeviceLocalBit,
            0,
            out _tlasBuffer, out _tlasMem, out _);

        VkAccelerationStructureCreateInfoKHR createInfo = new()
        {
            sType = VulkanConstants.StructureTypeAccelerationStructureCreateInfoKHR,
            pNext = null,
            createFlags = 0,
            buffer = _tlasBuffer,
            offset = 0,
            size = sizeInfo.accelerationStructureSize,
            type = 0, // TopLevel
            deviceAddress = 0
        };

        AccelerationStructureKHR tlasHandle;
        Result asRes = _pfnCreateAccelerationStructure(_device, &createInfo, null, &tlasHandle);
        if (asRes != Result.Success || tlasHandle.Handle == 0)
            throw new InvalidOperationException($"vkCreateAccelerationStructureKHR failed for TLAS: {asRes}");
        Tlas = tlasHandle;

        VkBuffer tlasScratchBuf;
        DeviceMemory tlasScratchMem;
        CreateDeviceAddressBuffer(
            sizeInfo.buildScratchSize + 256,
            BufferUsageFlags.StorageBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            0,
            out tlasScratchBuf, out tlasScratchMem, out ulong tlasScratchAddress);

        ulong alignedScratch = (tlasScratchAddress + 255UL) & ~255UL;
        buildInfo.dstAccelerationStructure = Tlas;
        buildInfo.scratchData.deviceAddress = alignedScratch;

        VkAccelerationStructureBuildRangeInfoKHR range = new()
        {
            primitiveCount = instanceCount,
            primitiveOffset = 0,
            firstVertex = 0,
            transformOffset = 0
        };
        VkAccelerationStructureBuildRangeInfoKHR* pRange = &range;

        CommandBuffer cb = BeginImmediateCommands();
        _pfnCmdBuildAccelerationStructures(cb, 1, &buildInfo, &pRange);
        EndImmediateCommands(cb);

        _vk.DestroyBuffer(_device, tlasScratchBuf, null);
        _vk.FreeMemory(_device, tlasScratchMem, null);
    }

    private CommandBuffer BeginImmediateCommands()
    {
        CommandBufferAllocateInfo cbAlloc = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _cmdPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        CommandBuffer cb;
        _vk.AllocateCommandBuffers(_device, &cbAlloc, &cb);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        _vk.BeginCommandBuffer(cb, &beginInfo);
        return cb;
    }

    private void EndImmediateCommands(CommandBuffer cb)
    {
        _vk.EndCommandBuffer(cb);

        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cb
        };

        _vk.QueueSubmit(_queue, 1, &submit, default);
        _vk.QueueWaitIdle(_queue);
        _vk.FreeCommandBuffers(_device, _cmdPool, 1, &cb);
    }

    private void Cleanup()
    {
        if (Tlas.Handle != 0) { _pfnDestroyAccelerationStructure(_device, Tlas, null); Tlas = default; }
        if (Blas.Handle != 0) { _pfnDestroyAccelerationStructure(_device, Blas, null); Blas = default; }

        if (_tlasBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _tlasBuffer, null); _tlasBuffer = default; }
        if (_tlasMem.Handle != 0) { _vk.FreeMemory(_device, _tlasMem, null); _tlasMem = default; }

        if (_blasBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _blasBuffer, null); _blasBuffer = default; }
        if (_blasMem.Handle != 0) { _vk.FreeMemory(_device, _blasMem, null); _blasMem = default; }

        if (_instBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _instBuffer, null); _instBuffer = default; }
        if (_instMem.Handle != 0) { _vk.FreeMemory(_device, _instMem, null); _instMem = default; }

        if (_scratchBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _scratchBuffer, null); _scratchBuffer = default; }
        if (_scratchMem.Handle != 0) { _vk.FreeMemory(_device, _scratchMem, null); _scratchMem = default; }

        if (_vtxBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _vtxBuffer, null); _vtxBuffer = default; }
        if (_vtxMem.Handle != 0) { _vk.FreeMemory(_device, _vtxMem, null); _vtxMem = default; }

        if (_idxBuffer.Handle != 0) { _vk.DestroyBuffer(_device, _idxBuffer, null); _idxBuffer = default; }
        if (_idxMem.Handle != 0) { _vk.FreeMemory(_device, _idxMem, null); _idxMem = default; }

        IsInitialized = false;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}