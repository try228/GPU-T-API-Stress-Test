namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Preset stress configurations targeting specific GPU execution pipelines and hardware domains.
/// </summary>
public enum StressPreset
{
    DefaultFP32,
    Gaming,
    GamingRayTracing,
    MatrixAI,
    MemoryCache,
    VideoEngine,
    FullSiliconBurn
}

/// <summary>
/// Represents a single selectable stress test item with metadata, hardware support status, and execution metrics.
/// </summary>
public sealed class StressTestItem
{
    /// <summary>
    /// Gets the unique string identifier for the workload.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the user-friendly display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the category grouping for UI matrix layout.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the underlying physical hardware supports this workload.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this test is currently selected for execution.
    /// </summary>
    public bool IsChecked { get; set; }

    /// <summary>
    /// Gets the measurement unit label (e.g., TFLOPS, TOPS, GB/s, ns).
    /// </summary>
    public string MetricUnit { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StressTestItem"/> class.
    /// </summary>
    public StressTestItem(string id, string name, string category, bool isSupported, string metricUnit, bool defaultChecked = false)
    {
        Id = id;
        Name = name;
        Category = category;
        IsSupported = isSupported;
        MetricUnit = metricUnit;
        IsChecked = isSupported && defaultChecked;
    }
}

/// <summary>
/// Central registry of all hardware workloads, categories, and automated stress presets.
/// </summary>
public sealed class VulkanTestRegistry
{
    /// <summary>
    /// Gets the complete list of registered stress test workloads.
    /// </summary>
    public List<StressTestItem> Items { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanTestRegistry"/> class, probing hardware capabilities.
    /// </summary>
    /// <param name="probe">The probed hardware capability descriptor.</param>
    public VulkanTestRegistry(VulkanCapabilityProbe probe)
    {
        // 1. COMPUTE ALU: FLOATING POINT
        Items.Add(new("alu_fp32", "FP32 Core FMA",     "ALU (FLOAT)", true, "TFLOPS", defaultChecked: true));
        Items.Add(new("alu_fp16", "FP16 Packed Math",  "ALU (FLOAT)", probe.HasFloat16, "TFLOPS"));
        Items.Add(new("alu_fp64", "FP64 Double Prec.", "ALU (FLOAT)", probe.HasFloat64, "GFLOPS"));

        // 2. COMPUTE ALU: INTEGER & DOT PRODUCT
        Items.Add(new("alu_int32", "INT32 Core Math",   "ALU (INT)", true, "GIOPS"));
        Items.Add(new("alu_int64", "INT64 Long Math",   "ALU (INT)", probe.HasInt64, "GIOPS"));
        Items.Add(new("alu_int16", "INT16 Short Math",  "ALU (INT)", probe.HasInt16, "GIOPS"));
        Items.Add(new("alu_int8",  "INT8 Byte Math",    "ALU (INT)", probe.HasInt8, "GIOPS"));
        Items.Add(new("alu_dp4a",  "INT8 DP4A DotProd", "ALU (INT)", probe.HasIntegerDotProduct, "TOPS"));
        Items.Add(new("alu_dp2a",  "INT16 DP2A DotProd","ALU (INT)", probe.HasIntegerDotProduct && probe.HasInt16, "TOPS"));

        // 3. VRAM & CACHES
        Items.Add(new("mem_bw",   "VRAM Stream BW",    "VRAM & CACHES", true, "GB/s"));
        Items.Add(new("mem_l1l2", "L1/L2 Cache BW",    "VRAM & CACHES", true, "GB/s"));
        Items.Add(new("mem_l3",   "L3/Infinity Cache", "VRAM & CACHES", probe.HasL3InfinityCache, "GB/s"));
        Items.Add(new("mem_lat",  "Pointer Latency",   "VRAM & CACHES", true, "ns"));

        // 4. MATRIX / TENSOR DATA TYPES (Strict hardware capability verification)
        Items.Add(new("mat_fp16",     "Matrix FP16 GEMM",   "MATRIX TYPES", probe.HasMatFP16, "TOPS"));
        Items.Add(new("mat_bf16",     "Matrix BF16 GEMM",   "MATRIX TYPES", probe.HasMatBF16, "TOPS"));
        Items.Add(new("mat_fp32",     "Matrix FP32 GEMM",   "MATRIX TYPES", probe.HasMatFP32, "TOPS"));
        Items.Add(new("mat_fp64",     "Matrix FP64 GEMM",   "MATRIX TYPES", probe.HasMatFP64, "TOPS"));
        Items.Add(new("mat_fp8_e4m3", "Matrix FP8 E4M3",   "MATRIX TYPES", probe.HasMatFP8_E4M3, "TOPS"));
        Items.Add(new("mat_fp8_e5m2", "Matrix FP8 E5M2",   "MATRIX TYPES", probe.HasMatFP8_E5M2, "TOPS"));
        Items.Add(new("mat_int8",     "Matrix INT8/UINT8",  "MATRIX TYPES", probe.HasMatINT8, "TOPS"));
        Items.Add(new("mat_int4",     "Matrix INT4 (Intel)","MATRIX TYPES", probe.HasIntelInt4, "TOPS"));
        Items.Add(new("mat_int16",    "Matrix INT16/U16",   "MATRIX TYPES", probe.HasMatINT16, "TOPS"));
        Items.Add(new("mat_int32",    "Matrix INT32/U32",   "MATRIX TYPES", probe.HasMatINT32, "TOPS"));
        Items.Add(new("mat_int64",    "Matrix INT64/U64",   "MATRIX TYPES", probe.HasMatINT64, "TOPS"));

        // 5. MATRIX MODIFIERS
        Items.Add(new("mat_mod_nv2",  "NV CoopMat 2 (Tensor)", "MATRIX MODS", probe.HasCoopMatrixNV2, "MODE"));
        Items.Add(new("mat_mod_nv1",  "Legacy NV CoopMat 1",   "MATRIX MODS", probe.HasCoopMatrixNV, "MODE"));

        // 6. RAY TRACING
        Items.Add(new("rt_query", "Ray Query (Inline)", "RAY TRACING", probe.HasRayQuery, "MRays/s"));
        Items.Add(new("rt_pipe",  "RT Pipeline (HW)",   "RAY TRACING", probe.HasRTPipeline, "MRays/s"));
        Items.Add(new("rt_ser",   "SER Reordering",     "RAY TRACING", probe.HasSER, "MRays/s"));
        Items.Add(new("rt_omm",   "OMM Opacity Map",    "RAY TRACING", probe.HasOMM, "MRays/s"));
        Items.Add(new("rt_pos",   "RT Position Fetch",  "RAY TRACING", probe.HasRTPositionFetch, "MRays/s"));
        Items.Add(new("rt_nv",    "Legacy NV_RT (2018)","RAY TRACING", probe.HasNVRayTracing, "MRays/s"));

        // 7. VULKAN VIDEO
        Items.Add(new("vid_dec_av1",  "Decode AV1",  "VULKAN VIDEO", probe.HasVideoDecodeAV1, "FPS"));
        Items.Add(new("vid_dec_hevc", "Decode H.265","VULKAN VIDEO", probe.HasVideoDecodeH265, "FPS"));
        Items.Add(new("vid_dec_h264", "Decode H.264","VULKAN VIDEO", probe.HasVideoDecodeH264, "FPS"));
        Items.Add(new("vid_dec_vp9",  "Decode VP9",  "VULKAN VIDEO", probe.HasVideoDecodeVP9, "FPS"));
        Items.Add(new("vid_enc_av1",  "Encode AV1",  "VULKAN VIDEO", probe.HasVideoEncodeAV1, "FPS"));
        Items.Add(new("vid_enc_hevc", "Encode H.265","VULKAN VIDEO", probe.HasVideoEncodeH265, "FPS"));
        Items.Add(new("vid_enc_h264", "Encode H.264","VULKAN VIDEO", probe.HasVideoEncodeH264, "FPS"));

        // 8. ROP: COLOR & MRT
        Items.Add(new("rop_rgba8",   "RGBA8 Fillrate",     "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_rgba16f", "RGBA16F HDR Rate",   "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_rgba32f", "RGBA32F Float Rate", "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_mrt2",    "MRT 2x Fillrate",    "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_mrt4",    "MRT 4x G-Buffer",    "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_mrt8",    "MRT 8x Max Sat.",    "ROP: COLOR & MRT", true, "GPixels/s"));
        Items.Add(new("rop_blend",   "Alpha Blend Stress", "ROP: COLOR & MRT", true, "GBlends/s"));
        Items.Add(new("rop_msaa",    "MSAA 8x Resolve",    "ROP: COLOR & MRT", true, "GPixels/s"));

        // 9. ROP: DEPTH & STENCIL
        Items.Add(new("ds_test",    "Depth Test (Z-Read)", "DEPTH / STENCIL", true, "GPixels/s"));
        Items.Add(new("ds_write",   "Depth Write Rate",    "DEPTH / STENCIL", true, "GPixels/s"));
        Items.Add(new("ds_earlyz",  "Early-Z Culling",     "DEPTH / STENCIL", true, "GPixels/s"));
        Items.Add(new("ds_hiz",     "Hi-Z Coarse Engine",  "DEPTH / STENCIL", true, "GPixels/s"));
        Items.Add(new("ds_stencil", "8-Bit Stencil Rate",  "DEPTH / STENCIL", true, "GPixels/s"));

        // 10. TMU TEXTURES
        Items.Add(new("tmu_bdt",   "Bilinear Filtering", "TMU TEXTURES", true, "GTexels/s"));
        Items.Add(new("tmu_aniso", "16x Anisotropic",    "TMU TEXTURES", true, "GTexels/s"));
        Items.Add(new("tmu_bc",    "BC1-BC7 Decompress", "TMU TEXTURES", true, "GTexels/s"));
        Items.Add(new("tmu_3d",    "3D/CubeMap Fetch",   "TMU TEXTURES", true, "GTexels/s"));
    }

    /// <summary>
    /// Checks whether a specific workload is both supported by hardware and currently selected.
    /// </summary>
    public bool IsActive(string id)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                return Items[i].IsChecked && Items[i].IsSupported;
        }
        return false;
    }

    /// <summary>
    /// Determines whether at least one supported workload is currently active.
    /// </summary>
    public bool HasActiveTests()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].IsChecked && Items[i].IsSupported)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the total number of currently active silicon test workloads.
    /// </summary>
    public int GetActiveCount()
    {
        int count = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].IsChecked && Items[i].IsSupported) count++;
        }
        return count;
    }

    /// <summary>
    /// Applies an automated preset selection across the test matrix.
    /// </summary>
    /// <param name="preset">The target stress preset to activate.</param>
    public void ApplyPreset(StressPreset preset)
    {
        foreach (var item in Items)
        {
            if (!item.IsSupported)
            {
                item.IsChecked = false;
                continue;
            }

            item.IsChecked = preset switch
            {
                StressPreset.DefaultFP32 => item.Id == "alu_fp32",
                StressPreset.Gaming => item.Category.Contains("ALU") || item.Category.Contains("ROP") || item.Category.Contains("TMU") || item.Category.Contains("VRAM") && !item.Id.Contains("64") && !item.Id.Contains("32f") && !item.Id.Contains("mrt8"),
                StressPreset.GamingRayTracing => item.Category.Contains("RAY TRACING") || item.Category.Contains("ALU") || item.Category.Contains("ROP") || item.Category.Contains("TMU") || item.Category.Contains("VRAM"),
                StressPreset.MatrixAI => item.Category.Contains("MATRIX") || item.Id == "alu_dp4a" || item.Id == "alu_dp2a" || item.Id == "alu_fp16",
                StressPreset.MemoryCache => item.Category == "VRAM & CACHES" || item.Id.Contains("mrt"),
                StressPreset.VideoEngine => item.Category == "VULKAN VIDEO",
                StressPreset.FullSiliconBurn => true,
                _ => item.IsChecked
            };
        }
    }
}