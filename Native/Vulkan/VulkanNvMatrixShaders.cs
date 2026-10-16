namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Dedicated provider for NVIDIA Matrix compute architectures:
/// VK_NV_cooperative_matrix (NV1 - Subgroup Scope) and
/// VK_NV_cooperative_matrix2 (NV2 - Workgroup Scope).
/// </summary>
public static class VulkanNvMatrixShaders
{
    // Bytecode caches for VK_NV_cooperative_matrix (NV1)
    private static uint[]? _spvNv1Fp16;
    private static uint[]? _spvNv1Fp32;
    private static uint[]? _spvNv1Fp64;
    private static uint[]? _spvNv1Int8;
    private static uint[]? _spvNv1Int16;
    private static uint[]? _spvNv1Int32;
    private static uint[]? _spvNv1Int64;

    // Bytecode caches for VK_NV_cooperative_matrix2 (NV2)
    private static uint[]? _spvNv2Fp16;
    private static uint[]? _spvNv2Bf16;
    private static uint[]? _spvNv2Fp8E4M3;
    private static uint[]? _spvNv2Fp8E5M2;
    private static uint[]? _spvNv2Fp32;
    private static uint[]? _spvNv2Fp64;
    private static uint[]? _spvNv2Int8;
    private static uint[]? _spvNv2Int16;
    private static uint[]? _spvNv2Int32;
    private static uint[]? _spvNv2Int64;

    // NV1 (GLSL)
    public static uint[]? GetShaderNV1_FP16(VulkanCapabilityProbe? probe = null) => _spvNv1Fp16  ??= VulkanShaders.LoadSpirv("mat_nv1_fp16");
    public static uint[]? GetShaderNV1_FP32(VulkanCapabilityProbe? probe = null) => _spvNv1Fp32  ??= VulkanShaders.LoadSpirv("mat_nv1_fp32");
    public static uint[]? GetShaderNV1_FP64(VulkanCapabilityProbe? probe = null) => _spvNv1Fp64  ??= VulkanShaders.LoadSpirv("mat_nv1_fp64");
    public static uint[]? GetShaderNV1_INT8(VulkanCapabilityProbe? probe = null) => _spvNv1Int8  ??= VulkanShaders.LoadSpirv("mat_nv1_int8");
    public static uint[]? GetShaderNV1_INT16(VulkanCapabilityProbe? probe = null)=> _spvNv1Int16 ??= VulkanShaders.LoadSpirv("mat_nv1_int16");
    public static uint[]? GetShaderNV1_INT32(VulkanCapabilityProbe? probe = null)=> _spvNv1Int32 ??= VulkanShaders.LoadSpirv("mat_nv1_int32");
    public static uint[]? GetShaderNV1_INT64(VulkanCapabilityProbe? probe = null)=> _spvNv1Int64 ??= VulkanShaders.LoadSpirv("mat_nv1_int64");

    // NV2 (Slang)
    public static uint[]? GetShaderNV2_FP16(VulkanCapabilityProbe? probe = null)    => _spvNv2Fp16    ??= VulkanShaders.LoadSpirv("mat_nv2_fp16");
    public static uint[]? GetShaderNV2_BF16(VulkanCapabilityProbe? probe = null)    => _spvNv2Bf16    ??= VulkanShaders.LoadSpirv("mat_nv2_bf16");
    public static uint[]? GetShaderNV2_FP8_E4M3(VulkanCapabilityProbe? probe = null)=> _spvNv2Fp8E4M3 ??= VulkanShaders.LoadSpirv("mat_nv2_fp8_e4m3");
    public static uint[]? GetShaderNV2_FP8_E5M2(VulkanCapabilityProbe? probe = null)=> _spvNv2Fp8E5M2 ??= VulkanShaders.LoadSpirv("mat_nv2_fp8_e5m2");
    public static uint[]? GetShaderNV2_FP32(VulkanCapabilityProbe? probe = null)    => _spvNv2Fp32    ??= VulkanShaders.LoadSpirv("mat_nv2_fp32");
    public static uint[]? GetShaderNV2_FP64(VulkanCapabilityProbe? probe = null)    => _spvNv2Fp64    ??= VulkanShaders.LoadSpirv("mat_nv2_fp64");
    public static uint[]? GetShaderNV2_INT8(VulkanCapabilityProbe? probe = null)    => _spvNv2Int8    ??= VulkanShaders.LoadSpirv("mat_nv2_int8");
    public static uint[]? GetShaderNV2_INT16(VulkanCapabilityProbe? probe = null)   => _spvNv2Int16   ??= VulkanShaders.LoadSpirv("mat_nv2_int16");
    public static uint[]? GetShaderNV2_INT32(VulkanCapabilityProbe? probe = null)   => _spvNv2Int32   ??= VulkanShaders.LoadSpirv("mat_nv2_int32");
    public static uint[]? GetShaderNV2_INT64(VulkanCapabilityProbe? probe = null)   => _spvNv2Int64   ??= VulkanShaders.LoadSpirv("mat_nv2_int64");

    public static void ResetCache()
    {
        _spvNv1Fp16 = null; _spvNv1Fp32 = null; _spvNv1Fp64 = null;
        _spvNv1Int8 = null; _spvNv1Int16 = null; _spvNv1Int32 = null; _spvNv1Int64 = null;

        _spvNv2Fp16 = null; _spvNv2Bf16 = null; _spvNv2Fp8E4M3 = null; _spvNv2Fp8E5M2 = null;
        _spvNv2Fp32 = null; _spvNv2Fp64 = null;
        _spvNv2Int8 = null; _spvNv2Int16 = null; _spvNv2Int32 = null; _spvNv2Int64 = null;
    }
}