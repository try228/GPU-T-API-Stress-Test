using System;
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Format = Silk.NET.Vulkan.Format;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Manages offscreen render targets, framebuffers, render passes, and graphics pipelines
/// for hardware ROP (Fillrate, MRT, Blending) and Depth/Stencil stress testing.
/// </summary>
public sealed unsafe class VulkanGraphicsManager : IDisposable
{
    public const int RenderWidth = 1920;
    public const int RenderHeight = 1080;
    public const double PixelsPerDraw = (double)RenderWidth * RenderHeight;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Queue _queue;
    private readonly CommandPool _cmdPool;
    private readonly PhysicalDeviceMemoryProperties _memProps;

    private Image[] _colorImagesRgba8 = new Image[8];
    private DeviceMemory[] _colorMemRgba8 = new DeviceMemory[8];
    private ImageView[] _colorViewsRgba8 = new ImageView[8];

    private Image _colorImageRgba16f;
    private DeviceMemory _colorMemRgba16f;
    private ImageView _colorViewRgba16f;

    private Image _colorImageRgba32f;
    private DeviceMemory _colorMemRgba32f;
    private ImageView _colorViewRgba32f;

    private Image _msaaImageRgba8;
    private DeviceMemory _msaaMemRgba8;
    private ImageView _msaaViewRgba8;

    private Image _depthImage;
    private DeviceMemory _depthMem;
    private ImageView _depthView;
    private Format _depthFormat;

    private RenderPass _rpSingleRgba8;
    private Framebuffer _fbSingleRgba8;

    private RenderPass _rpSingleRgba16f;
    private Framebuffer _fbSingleRgba16f;

    private RenderPass _rpSingleRgba32f;
    private Framebuffer _fbSingleRgba32f;

    private RenderPass _rpMrt2;
    private Framebuffer _fbMrt2;

    private RenderPass _rpMrt4;
    private Framebuffer _fbMrt4;

    private RenderPass _rpMrt8;
    private Framebuffer _fbMrt8;

    private RenderPass _rpMsaa8x;
    private Framebuffer _fbMsaa8x;

    private RenderPass _rpDepth;
    private Framebuffer _fbDepth;

    private PipelineLayout _pipelineLayout;

    private readonly Dictionary<string, (Pipeline Pipeline, ShaderModule Vert, ShaderModule Frag)> _pipelines = new();

    public bool IsInitialized { get; private set; }

    public VulkanGraphicsManager(
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

    public bool Initialize()
    {
        try
        {
            _depthFormat = FindSupportedDepthFormat();
            CreateImagesAndViews();
            CreateRenderPasses();
            CreateFramebuffers();
            CreatePipelineLayout();
            CreateGraphicsPipelines();

            IsInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[VulkanGraphicsManager] Graphics Pipeline initialization skipped: {ex.Message}");
            Console.ResetColor();
            Cleanup();
            return false;
        }
    }

    private Format FindSupportedDepthFormat()
    {
        Format[] candidates = [Format.D32SfloatS8Uint, Format.D24UnormS8Uint];
        foreach (var format in candidates)
        {
            FormatProperties props;
            _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, format, &props);
            if ((props.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) != 0)
                return format;
        }
        return Format.D32SfloatS8Uint;
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
        throw new InvalidOperationException("Failed to find compatible memory type for attachment.");
    }

    private void CreateAttachmentImage(
        int width, int height, Format format, ImageUsageFlags usage,
        SampleCountFlags samples, ImageAspectFlags aspect,
        out Image image, out DeviceMemory memory, out ImageView view)
    {
        ImageCreateInfo imgInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = samples,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        _vk.CreateImage(_device, &imgInfo, null, out image);

        MemoryRequirements reqs;
        _vk.GetImageMemoryRequirements(_device, image, &reqs);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = reqs.Size,
            MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        _vk.AllocateMemory(_device, &allocInfo, null, out memory);
        _vk.BindImageMemory(_device, image, memory, 0);

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(aspect, 0, 1, 0, 1)
        };

        _vk.CreateImageView(_device, &viewInfo, null, out view);
    }

    private void CreateImagesAndViews()
    {
        for (int i = 0; i < 8; i++)
        {
            CreateAttachmentImage(
                RenderWidth, RenderHeight, Format.R8G8B8A8Unorm,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransientAttachmentBit,
                SampleCountFlags.Count1Bit, ImageAspectFlags.ColorBit,
                out _colorImagesRgba8[i], out _colorMemRgba8[i], out _colorViewsRgba8[i]);
        }

        CreateAttachmentImage(
            RenderWidth, RenderHeight, Format.R16G16B16A16Sfloat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransientAttachmentBit,
            SampleCountFlags.Count1Bit, ImageAspectFlags.ColorBit,
            out _colorImageRgba16f, out _colorMemRgba16f, out _colorViewRgba16f);

        CreateAttachmentImage(
            RenderWidth, RenderHeight, Format.R32G32B32A32Sfloat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransientAttachmentBit,
            SampleCountFlags.Count1Bit, ImageAspectFlags.ColorBit,
            out _colorImageRgba32f, out _colorMemRgba32f, out _colorViewRgba32f);

        CreateAttachmentImage(
            RenderWidth, RenderHeight, Format.R8G8B8A8Unorm,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransientAttachmentBit,
            SampleCountFlags.Count8Bit, ImageAspectFlags.ColorBit,
            out _msaaImageRgba8, out _msaaMemRgba8, out _msaaViewRgba8);

        CreateAttachmentImage(
            RenderWidth, RenderHeight, _depthFormat,
            ImageUsageFlags.DepthStencilAttachmentBit | ImageUsageFlags.TransientAttachmentBit,
            SampleCountFlags.Count1Bit, ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit,
            out _depthImage, out _depthMem, out _depthView);
    }

    private RenderPass CreateSimpleRenderPass(Format[] colorFormats, Format? depthFormat = null, SampleCountFlags samples = SampleCountFlags.Count1Bit, bool resolve = false)
    {
        int totalAttachments = colorFormats.Length + (depthFormat.HasValue ? 1 : 0) + (resolve ? 1 : 0);
        AttachmentDescription* pAtts = stackalloc AttachmentDescription[totalAttachments];
        AttachmentReference* pColRefs = stackalloc AttachmentReference[colorFormats.Length];

        for (uint i = 0; i < colorFormats.Length; i++)
        {
            pAtts[i] = new AttachmentDescription
            {
                Format = colorFormats[i],
                Samples = samples,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ColorAttachmentOptimal
            };
            pColRefs[i] = new AttachmentReference { Attachment = i, Layout = ImageLayout.ColorAttachmentOptimal };
        }

        AttachmentReference depthRef = default;
        int nextIdx = colorFormats.Length;
        if (depthFormat.HasValue)
        {
            pAtts[nextIdx] = new AttachmentDescription
            {
                Format = depthFormat.Value,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };
            depthRef = new AttachmentReference { Attachment = (uint)nextIdx, Layout = ImageLayout.DepthStencilAttachmentOptimal };
            nextIdx++;
        }

        AttachmentReference resolveRef = default;
        if (resolve)
        {
            pAtts[nextIdx] = new AttachmentDescription
            {
                Format = Format.R8G8B8A8Unorm,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ColorAttachmentOptimal
            };
            resolveRef = new AttachmentReference { Attachment = (uint)nextIdx, Layout = ImageLayout.ColorAttachmentOptimal };
        }

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = (uint)colorFormats.Length,
            PColorAttachments = pColRefs,
            PDepthStencilAttachment = depthFormat.HasValue ? &depthRef : null,
            PResolveAttachments = resolve ? &resolveRef : null
        };

        RenderPassCreateInfo rpInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = (uint)totalAttachments,
            PAttachments = pAtts,
            SubpassCount = 1,
            PSubpasses = &subpass
        };

        RenderPass rp;
        _vk.CreateRenderPass(_device, &rpInfo, null, out rp);
        return rp;
    }

    private void CreateRenderPasses()
    {
        _rpSingleRgba8   = CreateSimpleRenderPass([Format.R8G8B8A8Unorm]);
        _rpSingleRgba16f = CreateSimpleRenderPass([Format.R16G16B16A16Sfloat]);
        _rpSingleRgba32f = CreateSimpleRenderPass([Format.R32G32B32A32Sfloat]);

        _rpMrt2 = CreateSimpleRenderPass([Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm]);
        _rpMrt4 = CreateSimpleRenderPass([Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm]);
        _rpMrt8 = CreateSimpleRenderPass([
            Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm,
            Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm, Format.R8G8B8A8Unorm
        ]);

        _rpMsaa8x = CreateSimpleRenderPass([Format.R8G8B8A8Unorm], null, SampleCountFlags.Count8Bit, resolve: true);
        _rpDepth  = CreateSimpleRenderPass([Format.R8G8B8A8Unorm], _depthFormat);
    }

    private Framebuffer CreateFb(RenderPass rp, ImageView[] views)
    {
        ImageView* pViews = stackalloc ImageView[views.Length];
        for (int i = 0; i < views.Length; i++) pViews[i] = views[i];

        FramebufferCreateInfo fbInfo = new()
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = rp,
            AttachmentCount = (uint)views.Length,
            PAttachments = pViews,
            Width = (uint)RenderWidth,
            Height = (uint)RenderHeight,
            Layers = 1
        };

        Framebuffer fb;
        _vk.CreateFramebuffer(_device, &fbInfo, null, out fb);
        return fb;
    }

    private void CreateFramebuffers()
    {
        _fbSingleRgba8   = CreateFb(_rpSingleRgba8, [_colorViewsRgba8[0]]);
        _fbSingleRgba16f = CreateFb(_rpSingleRgba16f, [_colorViewRgba16f]);
        _fbSingleRgba32f = CreateFb(_rpSingleRgba32f, [_colorViewRgba32f]);

        _fbMrt2 = CreateFb(_rpMrt2, [_colorViewsRgba8[0], _colorViewsRgba8[1]]);
        _fbMrt4 = CreateFb(_rpMrt4, [_colorViewsRgba8[0], _colorViewsRgba8[1], _colorViewsRgba8[2], _colorViewsRgba8[3]]);
        _fbMrt8 = CreateFb(_rpMrt8, _colorViewsRgba8);

        _fbMsaa8x = CreateFb(_rpMsaa8x, [_msaaViewRgba8, _colorViewsRgba8[0]]);
        _fbDepth  = CreateFb(_rpDepth, [_colorViewsRgba8[0], _depthView]);
    }

    private void CreatePipelineLayout()
    {
        PipelineLayoutCreateInfo plInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PSetLayouts = null
        };
        _vk.CreatePipelineLayout(_device, &plInfo, null, out _pipelineLayout);
    }

    private ShaderModule CreateModule(uint[] spv)
    {
        fixed (uint* pCode = spv)
        {
            ShaderModuleCreateInfo info = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)(spv.Length * sizeof(uint)),
                PCode = pCode
            };
            ShaderModule sm;
            _vk.CreateShaderModule(_device, &info, null, out sm);
            return sm;
        }
    }

    private (Pipeline Pipeline, ShaderModule VertModule, ShaderModule FragModule) BuildPipeline(
        RenderPass rp, uint colorAttachmentCount,
        uint[] vertSpv, uint[] fragSpv,
        bool enableBlend = false,
        SampleCountFlags samples = SampleCountFlags.Count1Bit,
        bool depthTest = false, bool depthWrite = false, CompareOp depthOp = CompareOp.Always,
        bool stencilTest = false, StencilOp stencilPass = StencilOp.Keep)
    {
        var vertModule = CreateModule(vertSpv);
        var fragModule = CreateModule(fragSpv);

        byte* pMain = stackalloc byte[] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };

        PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vertModule, PName = pMain };
        stages[1] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fragModule, PName = pMain };

        PipelineVertexInputStateCreateInfo vtxInput = new() { SType = StructureType.PipelineVertexInputStateCreateInfo };
        PipelineInputAssemblyStateCreateInfo inputAss = new() { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };

        Viewport vp = new(0, 0, RenderWidth, RenderHeight, 0.0f, 1.0f);
        Rect2D sc = new(new Offset2D(0, 0), new Extent2D((uint)RenderWidth, (uint)RenderHeight));
        PipelineViewportStateCreateInfo vpState = new() { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &vp, ScissorCount = 1, PScissors = &sc };

        PipelineRasterizationStateCreateInfo rast = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = PolygonMode.Fill,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.CounterClockwise,
            LineWidth = 1.0f
        };

        PipelineMultisampleStateCreateInfo msState = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = samples
        };

        PipelineDepthStencilStateCreateInfo dsState = new()
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = depthTest,
            DepthWriteEnable = depthWrite,
            DepthCompareOp = depthOp,
            StencilTestEnable = stencilTest,
            Front = new StencilOpState { CompareOp = CompareOp.Always, PassOp = stencilPass, WriteMask = 0xFF, Reference = 1 }
        };

        PipelineColorBlendAttachmentState* blendAtts = stackalloc PipelineColorBlendAttachmentState[(int)colorAttachmentCount];
        for (int i = 0; i < (int)colorAttachmentCount; i++)
        {
            blendAtts[i] = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = enableBlend,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
                AlphaBlendOp = BlendOp.Add
            };
        }

        PipelineColorBlendStateCreateInfo cbState = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = colorAttachmentCount,
            PAttachments = blendAtts
        };

        GraphicsPipelineCreateInfo pipeInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = stages,
            PVertexInputState = &vtxInput,
            PInputAssemblyState = &inputAss,
            PViewportState = &vpState,
            PRasterizationState = &rast,
            PMultisampleState = &msState,
            PDepthStencilState = &dsState,
            PColorBlendState = &cbState,
            Layout = _pipelineLayout,
            RenderPass = rp,
            Subpass = 0
        };

        Pipeline pipeline;
        _vk.CreateGraphicsPipelines(_device, default, 1, &pipeInfo, null, &pipeline);
        return (pipeline, vertModule, fragModule);
    }

    private void RegisterPass(string testId, Pipeline p, ShaderModule v, ShaderModule f)
    {
        if (p.Handle != 0) _pipelines[testId] = (p, v, f);
    }

    private void CreateGraphicsPipelines()
    {
        uint[] vertSpv = VulkanShaders.GetShaderVertFullscreen();

        var p1 = BuildPipeline(_rpSingleRgba8, 1, vertSpv, VulkanShaders.GetShaderFragRopRgba8());
        RegisterPass("rop_rgba8", p1.Pipeline, p1.VertModule, p1.FragModule);

        var p2 = BuildPipeline(_rpSingleRgba16f, 1, vertSpv, VulkanShaders.GetShaderFragRopRgba16f());
        RegisterPass("rop_rgba16f", p2.Pipeline, p2.VertModule, p2.FragModule);

        var p3 = BuildPipeline(_rpSingleRgba32f, 1, vertSpv, VulkanShaders.GetShaderFragRopRgba32f());
        RegisterPass("rop_rgba32f", p3.Pipeline, p3.VertModule, p3.FragModule);

        var p4 = BuildPipeline(_rpMrt2, 2, vertSpv, VulkanShaders.GetShaderFragRopMrt2());
        RegisterPass("rop_mrt2", p4.Pipeline, p4.VertModule, p4.FragModule);

        var p5 = BuildPipeline(_rpMrt4, 4, vertSpv, VulkanShaders.GetShaderFragRopMrt4());
        RegisterPass("rop_mrt4", p5.Pipeline, p5.VertModule, p5.FragModule);

        var p6 = BuildPipeline(_rpMrt8, 8, vertSpv, VulkanShaders.GetShaderFragRopMrt8());
        RegisterPass("rop_mrt8", p6.Pipeline, p6.VertModule, p6.FragModule);

        var p7 = BuildPipeline(_rpSingleRgba8, 1, vertSpv, VulkanShaders.GetShaderFragRopBlend(), enableBlend: true);
        RegisterPass("rop_blend", p7.Pipeline, p7.VertModule, p7.FragModule);

        var p8 = BuildPipeline(_rpMsaa8x, 1, vertSpv, VulkanShaders.GetShaderFragRopRgba8(), enableBlend: false, samples: SampleCountFlags.Count8Bit);
        RegisterPass("rop_msaa", p8.Pipeline, p8.VertModule, p8.FragModule);

        var p9 = BuildPipeline(_rpDepth, 1, vertSpv, VulkanShaders.GetShaderFragDsTest(), enableBlend: false, depthTest: true, depthWrite: false, depthOp: CompareOp.LessOrEqual);
        RegisterPass("ds_test", p9.Pipeline, p9.VertModule, p9.FragModule);

        var p10 = BuildPipeline(_rpDepth, 1, vertSpv, VulkanShaders.GetShaderFragDsTest(), enableBlend: false, depthTest: true, depthWrite: true, depthOp: CompareOp.Always);
        RegisterPass("ds_write", p10.Pipeline, p10.VertModule, p10.FragModule);

        var p11 = BuildPipeline(_rpDepth, 1, vertSpv, VulkanShaders.GetShaderFragDsEarlyZ(), enableBlend: false, depthTest: true, depthWrite: false, depthOp: CompareOp.Less);
        RegisterPass("ds_earlyz", p11.Pipeline, p11.VertModule, p11.FragModule);

        var p12 = BuildPipeline(_rpDepth, 1, vertSpv, VulkanShaders.GetShaderFragDsHiZ(), enableBlend: false, depthTest: true, depthWrite: true, depthOp: CompareOp.LessOrEqual);
        RegisterPass("ds_hiz", p12.Pipeline, p12.VertModule, p12.FragModule);

        var p13 = BuildPipeline(_rpDepth, 1, vertSpv, VulkanShaders.GetShaderFragDsTest(), enableBlend: false, stencilTest: true, stencilPass: StencilOp.Replace);
        RegisterPass("ds_stencil", p13.Pipeline, p13.VertModule, p13.FragModule);
    }

    public bool HasPipeline(string testId) => _pipelines.ContainsKey(testId);

    public void RecordGraphicsPass(CommandBuffer cb, string testId, int drawBatchCount)
    {
        if (!_pipelines.TryGetValue(testId, out var entry)) return;

        var (rp, fb) = testId switch
        {
            "rop_rgba8" or "rop_blend" => (_rpSingleRgba8, _fbSingleRgba8),
            "rop_rgba16f"              => (_rpSingleRgba16f, _fbSingleRgba16f),
            "rop_rgba32f"              => (_rpSingleRgba32f, _fbSingleRgba32f),
            "rop_mrt2"                 => (_rpMrt2, _fbMrt2),
            "rop_mrt4"                 => (_rpMrt4, _fbMrt4),
            "rop_mrt8"                 => (_rpMrt8, _fbMrt8),
            "rop_msaa"                 => (_rpMsaa8x, _fbMsaa8x),
            _                          => (_rpDepth, _fbDepth)
        };

        RenderPassBeginInfo rpBegin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = rp,
            Framebuffer = fb,
            RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)RenderWidth, (uint)RenderHeight))
        };

        _vk.CmdBeginRenderPass(cb, &rpBegin, SubpassContents.Inline);
        _vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, entry.Pipeline);

        for (int d = 0; d < drawBatchCount; d++)
        {
            _vk.CmdDraw(cb, 3, 1, 0, 0);
        }

        _vk.CmdEndRenderPass(cb);
    }

    private void Cleanup()
    {
        foreach (var entry in _pipelines.Values)
        {
            if (entry.Pipeline.Handle != 0) _vk.DestroyPipeline(_device, entry.Pipeline, null);
            if (entry.Vert.Handle != 0) _vk.DestroyShaderModule(_device, entry.Vert, null);
            if (entry.Frag.Handle != 0) _vk.DestroyShaderModule(_device, entry.Frag, null);
        }
        _pipelines.Clear();

        if (_pipelineLayout.Handle != 0) { _vk.DestroyPipelineLayout(_device, _pipelineLayout, null); _pipelineLayout = default; }

        if (_fbSingleRgba8.Handle != 0)   _vk.DestroyFramebuffer(_device, _fbSingleRgba8, null);
        if (_fbSingleRgba16f.Handle != 0) _vk.DestroyFramebuffer(_device, _fbSingleRgba16f, null);
        if (_fbSingleRgba32f.Handle != 0) _vk.DestroyFramebuffer(_device, _fbSingleRgba32f, null);
        if (_fbMrt2.Handle != 0)          _vk.DestroyFramebuffer(_device, _fbMrt2, null);
        if (_fbMrt4.Handle != 0)          _vk.DestroyFramebuffer(_device, _fbMrt4, null);
        if (_fbMrt8.Handle != 0)          _vk.DestroyFramebuffer(_device, _fbMrt8, null);
        if (_fbMsaa8x.Handle != 0)        _vk.DestroyFramebuffer(_device, _fbMsaa8x, null);
        if (_fbDepth.Handle != 0)         _vk.DestroyFramebuffer(_device, _fbDepth, null);

        if (_rpSingleRgba8.Handle != 0)   _vk.DestroyRenderPass(_device, _rpSingleRgba8, null);
        if (_rpSingleRgba16f.Handle != 0) _vk.DestroyRenderPass(_device, _rpSingleRgba16f, null);
        if (_rpSingleRgba32f.Handle != 0) _vk.DestroyRenderPass(_device, _rpSingleRgba32f, null);
        if (_rpMrt2.Handle != 0)          _vk.DestroyRenderPass(_device, _rpMrt2, null);
        if (_rpMrt4.Handle != 0)          _vk.DestroyRenderPass(_device, _rpMrt4, null);
        if (_rpMrt8.Handle != 0)          _vk.DestroyRenderPass(_device, _rpMrt8, null);
        if (_rpMsaa8x.Handle != 0)        _vk.DestroyRenderPass(_device, _rpMsaa8x, null);
        if (_rpDepth.Handle != 0)         _vk.DestroyRenderPass(_device, _rpDepth, null);

        for (int i = 0; i < 8; i++)
        {
            if (_colorViewsRgba8[i].Handle != 0) _vk.DestroyImageView(_device, _colorViewsRgba8[i], null);
            if (_colorImagesRgba8[i].Handle != 0) _vk.DestroyImage(_device, _colorImagesRgba8[i], null);
            if (_colorMemRgba8[i].Handle != 0) _vk.FreeMemory(_device, _colorMemRgba8[i], null);
        }

        if (_colorViewRgba16f.Handle != 0) _vk.DestroyImageView(_device, _colorViewRgba16f, null);
        if (_colorImageRgba16f.Handle != 0) _vk.DestroyImage(_device, _colorImageRgba16f, null);
        if (_colorMemRgba16f.Handle != 0) _vk.FreeMemory(_device, _colorMemRgba16f, null);

        if (_colorViewRgba32f.Handle != 0) _vk.DestroyImageView(_device, _colorViewRgba32f, null);
        if (_colorImageRgba32f.Handle != 0) _vk.DestroyImage(_device, _colorImageRgba32f, null);
        if (_colorMemRgba32f.Handle != 0) _vk.FreeMemory(_device, _colorMemRgba32f, null);

        if (_msaaViewRgba8.Handle != 0) _vk.DestroyImageView(_device, _msaaViewRgba8, null);
        if (_msaaImageRgba8.Handle != 0) _vk.DestroyImage(_device, _msaaImageRgba8, null);
        if (_msaaMemRgba8.Handle != 0) _vk.FreeMemory(_device, _msaaMemRgba8, null);

        if (_depthView.Handle != 0) _vk.DestroyImageView(_device, _depthView, null);
        if (_depthImage.Handle != 0) _vk.DestroyImage(_device, _depthImage, null);
        if (_depthMem.Handle != 0) _vk.FreeMemory(_device, _depthMem, null);

        IsInitialized = false;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}