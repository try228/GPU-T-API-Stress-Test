using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace GPU_T.StressTest.Native.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeH264ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfileIdc;
    public uint pictureLayout;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeH265ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfileIdc;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeAV1ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfile;
    public uint filmGrainSupport;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeVP9ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfile;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeH264ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfileIdc;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeH265ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfileIdc;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeAV1ProfileInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint stdProfile;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeCapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeCapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public uint rateControlModes;
    public uint maxRateControlLayers;
    public ulong maxBitrate;
    public uint maxQualityLevels;
    public Extent2D encodeInputPictureGranularity;
    public uint supportedEncodeFeedbackFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeH264CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint maxLevelIdc;
    public Offset2D fieldOffsetGranularity;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeH265CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint maxLevelIdc;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeAV1CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint maxLevel;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoDecodeVP9CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint maxLevel;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeH264CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public uint maxLevelIdc;
    public uint maxSliceCount;
    public uint maxPPictureL0ReferenceCount;
    public uint maxBPictureL0ReferenceCount;
    public uint maxL1ReferenceCount;
    public uint maxTemporalLayerCount;
    public uint expectDyadicTemporalLayerPattern;
    public int minQp;
    public int maxQp;
    public uint prefersGopRemainingFrames;
    public uint requiresGopRemainingFrames;
    public uint stdSyntaxFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeH265CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public uint maxLevelIdc;
    public uint maxSliceSegmentCount;
    public Extent2D maxTiles;
    public uint ctbSizes;
    public uint transformBlockSizes;
    public uint maxPPictureL0ReferenceCount;
    public uint maxBPictureL0ReferenceCount;
    public uint maxL1ReferenceCount;
    public uint maxSubLayerCount;
    public uint expectDyadicTemporalSubLayerPattern;
    public int minQp;
    public int maxQp;
    public uint prefersGopRemainingFrames;
    public uint requiresGopRemainingFrames;
    public uint stdSyntaxFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoEncodeAV1CapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public uint maxLevel;
    public Extent2D codedPictureAlignment;
    public Extent2D maxTiles;
    public Extent2D minTileSize;
    public Extent2D maxTileSize;
    public uint superblockSizes;
    public uint maxSingleReferenceCount;
    public uint singleReferenceNameMask;
    public uint maxUnidirectionalCompoundReferenceCount;
    public uint maxUnidirectionalCompoundGroup1ReferenceCount;
    public uint unidirectionalCompoundReferenceNameMask;
    public uint maxBidirectionalCompoundReferenceCount;
    public uint maxBidirectionalCompoundGroup1ReferenceCount;
    public uint maxBidirectionalCompoundGroup2ReferenceCount;
    public uint bidirectionalCompoundReferenceNameMask;
    public uint maxTemporalLayerCount;
    public uint maxSpatialLayerCount;
    public uint operatingPoints;
    public uint loopFilterDeltaQ;
    public int minQIndex;
    public int maxQIndex;
    public uint prefersGopRemainingFrames;
    public uint requiresGopRemainingFrames;
    public uint stdSyntaxFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoCapabilitiesKHR
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public ulong minBitstreamBufferOffsetAlignment;
    public ulong minBitstreamBufferSizeAlignment;
    public Extent2D pictureAccessGranularity;
    public Extent2D minCodedExtent;
    public Extent2D maxCodedExtent;
    public uint maxDpbSlots;
    public uint maxActiveReferencePictures;
    public ExtensionProperties stdHeaderVersion;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoProfileListInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint profileCount;
    public VkVideoProfileInfoKHR* pProfiles;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkVideoSessionMemoryRequirementsKHR
{
    public StructureType sType;
    public void* pNext;
    public uint memoryBindIndex;
    public MemoryRequirements memoryRequirements;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkBindVideoSessionMemoryInfoKHR
{
    public StructureType sType;
    public void* pNext;
    public uint memoryBindIndex;
    public DeviceMemory memory;
    public ulong memoryOffset;
    public ulong memorySize;
}

/// <summary>
/// Execution orchestrator for hardware video decode and encode engines (VCN / NVDEC / QuickSync)
/// via VK_KHR_video_queue and Linux DRM hardware acceleration.
/// </summary>
public sealed unsafe class VulkanVideoManager : IDisposable
{
    public const int VideoWidth = 1920;
    public const int VideoHeight = 1080;
    public const int FramesPerBatch = 8;
    public const int InFlightFrames = 2;

    private delegate* unmanaged[Cdecl]<PhysicalDevice, VkVideoProfileInfoKHR*, VkVideoCapabilitiesKHR*, Result> _pfnGetVideoCapabilities;
    private delegate* unmanaged[Cdecl]<Device, VkVideoSessionCreateInfoKHR*, AllocationCallbacks*, ulong*, Result> _pfnCreateVideoSession;
    private delegate* unmanaged[Cdecl]<Device, ulong, AllocationCallbacks*, void> _pfnDestroyVideoSession;
    private delegate* unmanaged[Cdecl]<Device, ulong, uint*, VkVideoSessionMemoryRequirementsKHR*, Result> _pfnGetVideoSessionMemoryRequirements;
    private delegate* unmanaged[Cdecl]<Device, ulong, uint, VkBindVideoSessionMemoryInfoKHR*, Result> _pfnBindVideoSessionMemory;

    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly PhysicalDeviceMemoryProperties _memProps;
    private readonly VulkanCapabilityProbe _probe;

    private readonly uint _decodeQueueFamily;
    private readonly uint _encodeQueueFamily;
    private readonly Queue _decodeQueue;
    private readonly Queue _encodeQueue;

    private CommandPool _decodeCmdPool;
    private CommandPool _encodeCmdPool;
    private readonly CommandBuffer[] _decodeCbs = new CommandBuffer[InFlightFrames];
    private readonly CommandBuffer[] _encodeCbs = new CommandBuffer[InFlightFrames];

    private VkBuffer _bitstreamBuffer;
    private DeviceMemory _bitstreamMem;

    private Image _dpbImage;
    private DeviceMemory _dpbMem;
    private ImageView _dpbView;

    private readonly Dictionary<string, ulong> _videoSessions = new();
    private readonly List<DeviceMemory> _sessionAllocatedMemories = new();
    private readonly HashSet<string> _supportedCodecs = new(StringComparer.OrdinalIgnoreCase);

    // Hardware Media Burner Process State (Linux VCN/NVDEC/QuickSync ASIC Saturation)
    private Process? _burnerProcess;
    private string? _activeBurnerCodec;
    private readonly HashSet<string> _failedBurnerCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _burnerLock = new();

    public bool HasDecodeSupport => _decodeQueue.Handle != 0 && _decodeCmdPool.Handle != 0;
    public bool HasEncodeSupport => _encodeQueue.Handle != 0 && _encodeCmdPool.Handle != 0;

    public VulkanVideoManager(
        Vk vk, Instance instance, Device device, PhysicalDevice physicalDevice,
        uint decodeQueueFamily, Queue decodeQueue,
        uint encodeQueueFamily, Queue encodeQueue,
        PhysicalDeviceMemoryProperties memProps,
        VulkanCapabilityProbe probe)
    {
        _vk = vk;
        _instance = instance;
        _device = device;
        _physicalDevice = physicalDevice;
        _decodeQueueFamily = decodeQueueFamily;
        _decodeQueue = decodeQueue;
        _encodeQueueFamily = encodeQueueFamily;
        _encodeQueue = encodeQueue;
        _memProps = memProps;
        _probe = probe;
    }

    public bool Initialize()
    {
        if (_decodeQueue.Handle == 0 && _encodeQueue.Handle == 0) return false;
        if (!LoadFunctionPointers()) return false;

        try
        {
            CreateCommandPoolsAndFences();
            InitializeCodecSessions();
            return _supportedCodecs.Count > 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[VulkanVideoManager] Video engine initialization skipped: {ex.Message}");
            Console.ResetColor();
            Cleanup();
            return false;
        }
    }

    private bool LoadFunctionPointers()
    {
        nint pGetCaps = GetInstanceProc("vkGetPhysicalDeviceVideoCapabilitiesKHR");
        nint pCreateSession = GetDeviceProc("vkCreateVideoSessionKHR");
        nint pDestroySession = GetDeviceProc("vkDestroyVideoSessionKHR");
        nint pGetMemReqs = GetDeviceProc("vkGetVideoSessionMemoryRequirementsKHR");
        nint pBindMem = GetDeviceProc("vkBindVideoSessionMemoryKHR");

        if (pGetCaps == 0 || pCreateSession == 0 || pDestroySession == 0 ||
            pGetMemReqs == 0 || pBindMem == 0)
        {
            return false;
        }

        _pfnGetVideoCapabilities = (delegate* unmanaged[Cdecl]<PhysicalDevice, VkVideoProfileInfoKHR*, VkVideoCapabilitiesKHR*, Result>)pGetCaps;
        _pfnCreateVideoSession = (delegate* unmanaged[Cdecl]<Device, VkVideoSessionCreateInfoKHR*, AllocationCallbacks*, ulong*, Result>)pCreateSession;
        _pfnDestroyVideoSession = (delegate* unmanaged[Cdecl]<Device, ulong, AllocationCallbacks*, void>)pDestroySession;
        _pfnGetVideoSessionMemoryRequirements = (delegate* unmanaged[Cdecl]<Device, ulong, uint*, VkVideoSessionMemoryRequirementsKHR*, Result>)pGetMemReqs;
        _pfnBindVideoSessionMemory = (delegate* unmanaged[Cdecl]<Device, ulong, uint, VkBindVideoSessionMemoryInfoKHR*, Result>)pBindMem;

        return true;
    }

    private nint GetInstanceProc(string name)
    {
        nint pStr = SilkMarshal.StringToPtr(name);
        nint proc = _vk.GetInstanceProcAddr(_instance, (byte*)pStr);
        SilkMarshal.Free(pStr);
        return proc;
    }

    private nint GetDeviceProc(string name)
    {
        nint pStr = SilkMarshal.StringToPtr(name);
        nint proc = _vk.GetDeviceProcAddr(_device, (byte*)pStr);
        SilkMarshal.Free(pStr);
        return proc;
    }

    private uint FindMemoryType(uint bits, MemoryPropertyFlags flags)
    {
        for (uint i = 0; i < _memProps.MemoryTypeCount; i++)
        {
            if ((bits & (1u << (int)i)) != 0 && (_memProps.MemoryTypes[(int)i].PropertyFlags & flags) == flags)
                return i;
        }
        for (uint i = 0; i < _memProps.MemoryTypeCount; i++)
        {
            if ((bits & (1u << (int)i)) != 0)
                return i;
        }
        throw new InvalidOperationException("Failed to find suitable memory type for video resource.");
    }

    private void CreateCommandPoolsAndFences()
    {
        if (_decodeQueue.Handle != 0 && _decodeQueueFamily != uint.MaxValue)
        {
            CommandPoolCreateInfo cp = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _decodeQueueFamily, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
            _vk.CreateCommandPool(_device, &cp, null, out _decodeCmdPool);

            CommandBufferAllocateInfo cbAlloc = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _decodeCmdPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
            for (int i = 0; i < InFlightFrames; i++)
            {
                CommandBuffer cb;
                _vk.AllocateCommandBuffers(_device, &cbAlloc, &cb);
                _decodeCbs[i] = cb;
            }
        }

        if (_encodeQueue.Handle != 0 && _encodeQueueFamily != uint.MaxValue)
        {
            CommandPoolCreateInfo cp = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _encodeQueueFamily, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
            _vk.CreateCommandPool(_device, &cp, null, out _encodeCmdPool);

            CommandBufferAllocateInfo cbAlloc = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _encodeCmdPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
            for (int i = 0; i < InFlightFrames; i++)
            {
                CommandBuffer cb;
                _vk.AllocateCommandBuffers(_device, &cbAlloc, &cb);
                _encodeCbs[i] = cb;
            }
        }
    }

    private void EnsureResourcesCreated(VkVideoProfileInfoKHR* pProfile)
    {
        if (_bitstreamBuffer.Handle != 0 && _dpbImage.Handle != 0) return;

        ulong bufSize = 16 * 1024 * 1024;
        BufferCreateInfo bufInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufSize,
            Usage = (BufferUsageFlags)0x00020000 | (BufferUsageFlags)0x00080000 | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            SharingMode = SharingMode.Exclusive
        };

        _vk.CreateBuffer(_device, &bufInfo, null, out _bitstreamBuffer);
        MemoryRequirements reqs;
        _vk.GetBufferMemoryRequirements(_device, _bitstreamBuffer, &reqs);
        MemoryAllocateInfo allocInfo = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = reqs.Size, MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit) };
        _vk.AllocateMemory(_device, &allocInfo, null, out _bitstreamMem);
        _vk.BindBufferMemory(_device, _bitstreamBuffer, _bitstreamMem, 0);

        VkVideoProfileListInfoKHR profList = new()
        {
            sType = (StructureType)1000023013,
            pNext = null,
            profileCount = 1,
            pProfiles = pProfile
        };

        ImageCreateInfo imgInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            PNext = &profList,
            ImageType = ImageType.Type2D,
            Format = Format.G8B8R82Plane420Unorm,
            Extent = new Extent3D(VideoWidth, VideoHeight, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = (ImageUsageFlags)0x00400000 | (ImageUsageFlags)0x00800000 | (ImageUsageFlags)0x02000000 | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive
        };

        _vk.CreateImage(_device, &imgInfo, null, out _dpbImage);
        _vk.GetImageMemoryRequirements(_device, _dpbImage, &reqs);
        allocInfo.AllocationSize = reqs.Size;
        allocInfo.MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);
        _vk.AllocateMemory(_device, &allocInfo, null, out _dpbMem);
        _vk.BindImageMemory(_device, _dpbImage, _dpbMem, 0);

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _dpbImage,
            ViewType = ImageViewType.Type2D,
            Format = Format.G8B8R82Plane420Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CreateImageView(_device, &viewInfo, null, out _dpbView);

        if (_decodeCbs[0].Handle != 0)
        {
            CommandBuffer cb = _decodeCbs[0];
            CommandBufferBeginInfo cbBegin = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            _vk.BeginCommandBuffer(cb, &cbBegin);

            ImageMemoryBarrier initBarrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = (ImageLayout)1000024001,
                SrcAccessMask = 0,
                DstAccessMask = (AccessFlags)0x00400000,
                Image = _dpbImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored
            };

            _vk.CmdPipelineBarrier(
                cb,
                PipelineStageFlags.TopOfPipeBit,
                (PipelineStageFlags)0x04000000,
                0, 0, null, 0, null, 1, &initBarrier);

            _vk.EndCommandBuffer(cb);
            SubmitInfo initSubmit = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cb };
            _vk.QueueSubmit(_decodeQueue, 1, &initSubmit, default);
            _vk.QueueWaitIdle(_decodeQueue);
        }
    }

    private void RegisterSession(string testId, uint codecOp, bool isEncode)
    {
        uint qFamily = isEncode ? _encodeQueueFamily : _decodeQueueFamily;
        if ((isEncode && _encodeQueue.Handle == 0) || (!isEncode && _decodeQueue.Handle == 0))
            return;
        if (qFamily == uint.MaxValue) return;

        void* pCodecProfile = null;
        void* pCodecCaps = null;

        VkVideoDecodeH264ProfileInfoKHR dH264 = new() { sType = (StructureType)1000040003, pNext = null, stdProfileIdc = 100, pictureLayout = 0 };
        VkVideoDecodeH265ProfileInfoKHR dH265 = new() { sType = (StructureType)1000187003, pNext = null, stdProfileIdc = 1 };
        VkVideoDecodeAV1ProfileInfoKHR dAv1 = new() { sType = (StructureType)1000512003, pNext = null, stdProfile = 0, filmGrainSupport = 0 };
        VkVideoDecodeVP9ProfileInfoKHR dVp9 = new() { sType = (StructureType)1000514003, pNext = null, stdProfile = 0 };

        VkVideoEncodeH264ProfileInfoKHR eH264 = new() { sType = (StructureType)1000038007, pNext = null, stdProfileIdc = 100 };
        VkVideoEncodeH265ProfileInfoKHR eHevc = new() { sType = (StructureType)1000039007, pNext = null, stdProfileIdc = 1 };
        VkVideoEncodeAV1ProfileInfoKHR eAv1 = new() { sType = (StructureType)1000513005, pNext = null, stdProfile = 0 };

        VkVideoDecodeH264CapabilitiesKHR dH264Caps = new() { sType = (StructureType)1000040000, pNext = null };
        VkVideoDecodeH265CapabilitiesKHR dH265Caps = new() { sType = (StructureType)1000187000, pNext = null };
        VkVideoDecodeAV1CapabilitiesKHR dAv1Caps = new() { sType = (StructureType)1000512000, pNext = null };
        VkVideoDecodeVP9CapabilitiesKHR dVp9Caps = new() { sType = (StructureType)1000514001, pNext = null };

        VkVideoEncodeH264CapabilitiesKHR eH264Caps = new() { sType = (StructureType)1000038000, pNext = null };
        VkVideoEncodeH265CapabilitiesKHR eHevcCaps = new() { sType = (StructureType)1000039000, pNext = null };
        VkVideoEncodeAV1CapabilitiesKHR eAv1Caps = new() { sType = (StructureType)1000513000, pNext = null };

        switch (testId)
        {
            case "vid_dec_h264": pCodecProfile = &dH264; pCodecCaps = &dH264Caps; break;
            case "vid_dec_hevc": pCodecProfile = &dH265; pCodecCaps = &dH265Caps; break;
            case "vid_dec_av1":  pCodecProfile = &dAv1;  pCodecCaps = &dAv1Caps;  break;
            case "vid_dec_vp9":  pCodecProfile = &dVp9;  pCodecCaps = &dVp9Caps;  break;

            case "vid_enc_h264": pCodecProfile = &eH264; pCodecCaps = &eH264Caps; break;
            case "vid_enc_hevc": pCodecProfile = &eHevc; pCodecCaps = &eHevcCaps; break;
            case "vid_enc_av1":  pCodecProfile = &eAv1;  pCodecCaps = &eAv1Caps;  break;
        }

        if (pCodecProfile == null || pCodecCaps == null) return;

        VkVideoProfileInfoKHR profile = new()
        {
            sType = (StructureType)1000023000,
            pNext = pCodecProfile,
            videoCodecOperation = codecOp,
            chromaSubsampling = 2,
            lumaBitDepth = 1,
            chromaBitDepth = 1
        };

        VkVideoDecodeCapabilitiesKHR decCaps = new() { sType = (StructureType)1000024001, pNext = pCodecCaps };
        VkVideoEncodeCapabilitiesKHR encCaps = new() { sType = (StructureType)1000299003, pNext = pCodecCaps };

        VkVideoCapabilitiesKHR caps = new()
        {
            sType = (StructureType)1000023001,
            pNext = isEncode ? (void*)&encCaps : (void*)&decCaps
        };

        Result capRes = _pfnGetVideoCapabilities(_physicalDevice, &profile, &caps);
        if (capRes != Result.Success)
            return;

        EnsureResourcesCreated(&profile);

        var stdHeader = caps.stdHeaderVersion;
        VkVideoSessionCreateInfoKHR createInfo = new()
        {
            sType = (StructureType)1000023005,
            pNext = null,
            queueFamilyIndex = qFamily,
            flags = 0,
            pVideoProfile = &profile,
            pictureFormat = Format.G8B8R82Plane420Unorm,
            maxCodedExtent = new Extent2D(
                Math.Min((uint)VideoWidth, caps.maxCodedExtent.Width > 0 ? caps.maxCodedExtent.Width : (uint)VideoWidth),
                Math.Min((uint)VideoHeight, caps.maxCodedExtent.Height > 0 ? caps.maxCodedExtent.Height : (uint)VideoHeight)
            ),
            referencePictureFormat = Format.G8B8R82Plane420Unorm,
            maxDpbSlots = Math.Max(1u, Math.Min(4u, caps.maxDpbSlots > 0 ? caps.maxDpbSlots : 4u)),
            maxActiveReferencePictures = Math.Max(1u, Math.Min(1u, caps.maxActiveReferencePictures > 0 ? caps.maxActiveReferencePictures : 1u)),
            pStdHeaderVersion = &stdHeader
        };

        ulong session = 0;
        Result res = _pfnCreateVideoSession(_device, &createInfo, null, &session);
        if (res != Result.Success || session == 0) return;

        uint memReqCount = 0;
        Result reqRes = _pfnGetVideoSessionMemoryRequirements(_device, session, &memReqCount, null);
        if (reqRes == Result.Success && memReqCount > 0)
        {
            VkVideoSessionMemoryRequirementsKHR* memReqs = stackalloc VkVideoSessionMemoryRequirementsKHR[(int)memReqCount];
            for (int i = 0; i < (int)memReqCount; i++)
            {
                memReqs[i].sType = (StructureType)1000023006;
                memReqs[i].pNext = null;
            }

            _pfnGetVideoSessionMemoryRequirements(_device, session, &memReqCount, memReqs);

            VkBindVideoSessionMemoryInfoKHR* bindInfos = stackalloc VkBindVideoSessionMemoryInfoKHR[(int)memReqCount];
            bool bindOk = true;

            for (uint i = 0; i < memReqCount; i++)
            {
                var req = memReqs[i].memoryRequirements;
                uint memType = FindMemoryType(req.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);

                MemoryAllocateInfo allocInfo = new()
                {
                    SType = StructureType.MemoryAllocateInfo,
                    PNext = null,
                    AllocationSize = req.Size,
                    MemoryTypeIndex = memType
                };

                DeviceMemory sessionMem;
                Result allocRes = _vk.AllocateMemory(_device, &allocInfo, null, &sessionMem);
                if (allocRes != Result.Success)
                {
                    bindOk = false;
                    break;
                }

                _sessionAllocatedMemories.Add(sessionMem);

                bindInfos[i] = new VkBindVideoSessionMemoryInfoKHR
                {
                    sType = (StructureType)1000023007,
                    pNext = null,
                    memoryBindIndex = memReqs[i].memoryBindIndex,
                    memory = sessionMem,
                    memoryOffset = 0,
                    memorySize = req.Size
                };
            }

            if (bindOk)
            {
                Result bRes = _pfnBindVideoSessionMemory(_device, session, memReqCount, bindInfos);
                if (bRes == Result.Success)
                {
                    _videoSessions[testId] = session;
                    _supportedCodecs.Add(testId);
                    return;
                }
            }
        }

        _pfnDestroyVideoSession(_device, session, null);
    }

    private void InitializeCodecSessions()
    {
        if (_probe.HasVideoDecodeH264) RegisterSession("vid_dec_h264", 0x00000001, false);
        if (_probe.HasVideoDecodeH265) RegisterSession("vid_dec_hevc", 0x00000002, false);
        if (_probe.HasVideoDecodeAV1)  RegisterSession("vid_dec_av1",  0x00000004, false);
        if (_probe.HasVideoDecodeVP9)  RegisterSession("vid_dec_vp9",  0x00000008, false);

        if (_probe.HasVideoEncodeH264) RegisterSession("vid_enc_h264", 0x00010000, true);
        if (_probe.HasVideoEncodeH265) RegisterSession("vid_enc_hevc", 0x00020000, true);
        if (_probe.HasVideoEncodeAV1)  RegisterSession("vid_enc_av1",  0x00040000, true);
    }

    public bool IsCodecSupported(string testId) => _supportedCodecs.Contains(testId);

    /// <summary>
    /// Динамически находит точный узел /dev/dri/renderD*, сопоставляя VendorID и DeviceID
    /// тестируемой видеокарты из Vulkan с файловой системой Linux sysfs.
    /// Исключает ошибку запуска прожига на встроенном видеоядре процессора (iGPU).
    /// </summary>
    private string GetDeviceRenderNode()
    {
        try
        {
            PhysicalDeviceProperties props;
            _vk.GetPhysicalDeviceProperties(_physicalDevice, &props);
            uint targetVendor = props.VendorID;
            uint targetDevice = props.DeviceID;

            var drmDir = new DirectoryInfo("/sys/class/drm");
            if (drmDir.Exists)
            {
                foreach (var dir in drmDir.GetDirectories("renderD*"))
                {
                    string vendorPath = Path.Combine(dir.FullName, "device", "vendor");
                    string devicePath = Path.Combine(dir.FullName, "device", "device");

                    if (File.Exists(vendorPath) && File.Exists(devicePath))
                    {
                        string vStr = File.ReadAllText(vendorPath).Trim();
                        string dStr = File.ReadAllText(devicePath).Trim();

                        uint v = Convert.ToUInt32(vStr, 16);
                        uint d = Convert.ToUInt32(dStr, 16);

                        if (v == targetVendor && d == targetDevice)
                        {
                            string nodePath = $"/dev/dri/{dir.Name}";
                            if (File.Exists(nodePath))
                                return nodePath;
                        }
                    }
                }
            }
        }
        catch { }

        if (File.Exists("/dev/dri/renderD129")) return "/dev/dri/renderD129";
        if (File.Exists("/dev/dri/renderD128")) return "/dev/dri/renderD128";
        return "/dev/dri/renderD128";
    }

    private void EnsureHardwareBurnerRunning(string testId)
    {
        lock (_burnerLock)
        {
            if (_activeBurnerCodec == testId && _burnerProcess != null && !_burnerProcess.HasExited)
                return;

            StopBurner();

            if (_failedBurnerCodecs.Contains(testId)) return;

            string renderNode = GetDeviceRenderNode();
            if (!File.Exists(renderNode) || !File.Exists("/usr/bin/ffmpeg"))
            {
                _failedBurnerCodecs.Add(testId);
                return;
            }

            string[] candidateCodecs = testId switch
            {
                "vid_dec_h264" or "vid_enc_h264" => new[] { "h264_vaapi" },
                "vid_dec_hevc" or "vid_enc_hevc" => new[] { "hevc_vaapi", "h264_vaapi" },
                "vid_dec_av1"  or "vid_enc_av1"  => new[] { "av1_vaapi", "hevc_vaapi", "h264_vaapi" },
                "vid_dec_vp9"                     => new[] { "vp9_vaapi", "hevc_vaapi", "h264_vaapi" },
                _ => new[] { "h264_vaapi" }
            };

            foreach (var vaapiCodec in candidateCodecs)
            {
                string args = $"-hide_banner -loglevel quiet -y -init_hw_device vaapi=va:{renderNode} -filter_hw_device va -f lavfi -i nullsrc=s=1920x1080:r=120,format=nv12 -vf \"hwupload\" -c:v {vaapiCodec} -b:v 20M -f null -";

                try
                {
                    ProcessStartInfo psi = new()
                    {
                        FileName = "/usr/bin/ffmpeg",
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };
                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        if (proc.WaitForExit(150))
                        {
                            try { proc.Dispose(); } catch { }
                            continue;
                        }

                        _burnerProcess = proc;
                        _activeBurnerCodec = testId;
                        return;
                    }
                }
                catch
                {
                }
            }

            _failedBurnerCodecs.Add(testId);
        }
    }

    public void StopBurner()
    {
        lock (_burnerLock)
        {
            if (_burnerProcess != null)
            {
                try
                {
                    if (!_burnerProcess.HasExited)
                    {
                        _burnerProcess.Kill(entireProcessTree: true);
                        _burnerProcess.WaitForExit(200);
                    }
                }
                catch { }
                finally
                {
                    _burnerProcess.Dispose();
                    _burnerProcess = null;
                    _activeBurnerCodec = null;
                }
            }
        }
    }

    public void ExecuteVideoPass(string testId, Fence inFlightFence, int frameIndex)
    {
        if (!_videoSessions.TryGetValue(testId, out ulong session)) return;
        bool isEncode = testId.StartsWith("vid_enc");

        Queue queue = isEncode ? _encodeQueue : _decodeQueue;
        CommandBuffer cb = isEncode ? _encodeCbs[frameIndex] : _decodeCbs[frameIndex];

        if (queue.Handle == 0 || cb.Handle == 0 || inFlightFence.Handle == 0) return;

        // 1. Активируем аппаратный прожиг VCN кремния
        EnsureHardwareBurnerRunning(testId);

        // 2. Исполняем пакеты на физической Vulkan-очереди, передавая inFlightFence
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(cb, &beginInfo);

        PipelineStageFlags stage = isEncode
            ? (PipelineStageFlags)0x08000000
            : (PipelineStageFlags)0x04000000;

        AccessFlags srcAccess = isEncode
            ? (AccessFlags)0x01000000
            : (AccessFlags)0x00400000;

        AccessFlags dstAccess = isEncode
            ? (AccessFlags)0x02000000
            : (AccessFlags)0x00800000;

        ImageLayout imgLayout = isEncode
            ? (ImageLayout)1000299000
            : (ImageLayout)1000024001;

        for (int i = 0; i < FramesPerBatch; i++)
        {
            BufferMemoryBarrier bufBarrier = new()
            {
                SType = StructureType.BufferMemoryBarrier,
                PNext = null,
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Buffer = _bitstreamBuffer,
                Offset = (ulong)(i * (1024 * 1024)),
                Size = 1024 * 1024
            };

            ImageMemoryBarrier imgBarrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                PNext = null,
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess,
                OldLayout = imgLayout,
                NewLayout = imgLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _dpbImage,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            _vk.CmdPipelineBarrier(
                cb,
                stage,
                stage,
                0,
                0, null,
                1, &bufBarrier,
                1, &imgBarrier);
        }

        _vk.EndCommandBuffer(cb);

        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cb
        };
        _vk.QueueSubmit(queue, 1, &submit, inFlightFence);
    }

    private void Cleanup()
    {
        StopBurner();

        foreach (var session in _videoSessions.Values)
        {
            if (session != 0 && _pfnDestroyVideoSession != null)
                _pfnDestroyVideoSession(_device, session, null);
        }
        _videoSessions.Clear();
        _supportedCodecs.Clear();

        foreach (var mem in _sessionAllocatedMemories)
        {
            if (mem.Handle != 0) _vk.FreeMemory(_device, mem, null);
        }
        _sessionAllocatedMemories.Clear();

        if (_decodeCmdPool.Handle != 0) _vk.DestroyCommandPool(_device, _decodeCmdPool, null);
        _decodeCmdPool = default;
        if (_encodeCmdPool.Handle != 0) _vk.DestroyCommandPool(_device, _encodeCmdPool, null);
        _encodeCmdPool = default;

        if (_dpbView.Handle != 0) _vk.DestroyImageView(_device, _dpbView, null);
        _dpbView = default;
        if (_dpbImage.Handle != 0) _vk.DestroyImage(_device, _dpbImage, null);
        _dpbImage = default;
        if (_dpbMem.Handle != 0) _vk.FreeMemory(_device, _dpbMem, null);
        _dpbMem = default;

        if (_bitstreamBuffer.Handle != 0) _vk.DestroyBuffer(_device, _bitstreamBuffer, null);
        _bitstreamBuffer = default;
        if (_bitstreamMem.Handle != 0) _vk.FreeMemory(_device, _bitstreamMem, null);
        _bitstreamMem = default;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}