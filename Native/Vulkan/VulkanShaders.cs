using System.Reflection;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Hardware compute shader provider loading precompiled SPIR-V 1.0 (ALU, cache, TMU, VRAM)
/// and SPIR-V 1.3 (cooperative matrix) modules embedded into the executable assembly.
/// </summary>
public static class VulkanShaders
{
    /// <summary>Standard compute workgroup thread count.</summary>
    public const uint LocalSizeX = 64;

    // Mathematical operations per thread invocation across ALU domains
    public const double FP32_OPS_PER_INVOCATION = 4096.0;
    public const double FP16_OPS_PER_INVOCATION = 8192.0;
    public const double BF16_OPS_PER_INVOCATION = 4096.0;
    public const double FP64_OPS_PER_INVOCATION = 1024.0;

    public const double INT32_OPS_PER_INVOCATION = 4096.0;
    public const double INT16_OPS_PER_INVOCATION = 8192.0;
    public const double INT8_OPS_PER_INVOCATION  = 16384.0;
    public const double INT64_OPS_PER_INVOCATION = 1024.0;
    public const double DP4A_OPS_PER_INVOCATION  = 4096.0;
    public const double DP2A_OPS_PER_INVOCATION  = 2048.0;

    // TMU Texture units
    public const double TMU_SAMPLES_PER_INVOCATION = 256.0;

    // Fallback constants for Matrix workloads
    public const double MAT_FP32_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_FP64_OPS_PER_INVOCATION = 1024.0;
    public const double MAT_FP16_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_BF16_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_FP8_OPS_PER_INVOCATION  = 8192.0;
    public const double MAT_INT8_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_INT4_OPS_PER_INVOCATION = 8192.0;
    public const double MAT_INT16_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_INT32_OPS_PER_INVOCATION = 4096.0;
    public const double MAT_INT64_OPS_PER_INVOCATION = 1024.0;

    private static uint[]? _spvInitGpu;
    private static uint[]? _spvFp32, _spvFp64, _spvFp16, _spvBf16;
    private static uint[]? _spvInt32, _spvInt64, _spvInt16, _spvInt8;
    private static uint[]? _spvDp4a, _spvDp2a;

    private static uint[]? _spvMatFp32, _spvMatFp64, _spvMatFp16, _spvMatBf16;
    private static uint[]? _spvMatFp8E4M3, _spvMatFp8E5M2;
    private static uint[]? _spvMatInt8, _spvMatInt4, _spvMatInt16, _spvMatInt32, _spvMatInt64;

    private static uint[]? _spvMemL2, _spvMemL3, _spvMemVram, _spvPointerChase;
    private static uint[]? _spvTmuBdt, _spvTmuAniso, _spvTmuBc, _spvTmu3D;

    /// <summary>
    /// Loads aligned 32-bit SPIR-V binary words embedded into the assembly manifest or from build output.
    /// Guarantees fail-safe loading with descriptive diagnostics.
    /// </summary>
    /// <param name="shaderFileName">Base filename of the shader without extension (e.g. "alu_fp32").</param>
    /// <returns>An array of 32-bit unsigned integer words representing the SPIR-V module.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the shader binary cannot be located.</exception>
    public static uint[] LoadSpirv(string shaderFileName)
    {
        var assembly = typeof(VulkanShaders).Assembly;
        string resourceName = $"Shaders.{shaderFileName}.spv";

        // 1. Primary: load from embedded assembly resources
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            byte[] bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);

            if (bytes.Length == 0 || bytes.Length % 4 != 0)
            {
                throw new InvalidOperationException($"Corrupt SPIR-V binary alignment for '{resourceName}' ({bytes.Length} bytes).");
            }

            uint[] words = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            return words;
        }

        // 2. Fallback: check obj/spv output directory directly on disk
        string diskPath = Path.Combine("obj", "Debug", "net10.0", "spv", $"{shaderFileName}.spv");
        if (!File.Exists(diskPath))
        {
            diskPath = Path.Combine("obj", "Release", "net10.0", "spv", $"{shaderFileName}.spv");
        }

        if (File.Exists(diskPath))
        {
            byte[] bytes = File.ReadAllBytes(diskPath);
            uint[] words = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            return words;
        }

        // 3. Diagnostics
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Shader Error] Failed to locate '{resourceName}'!");
        Console.WriteLine($"Available embedded resources: [{string.Join(", ", assembly.GetManifestResourceNames())}]");
        Console.ResetColor();

        throw new InvalidOperationException($"SPIR-V module '{resourceName}' was neither embedded nor found on disk.");
    }

    // Initialization
    public static uint[] GetShaderInitBuffer() => _spvInitGpu ??= LoadSpirv("init_buffer")!;

    // ALU Compute
    public static uint[] GetShaderFP32() => _spvFp32 ??= LoadSpirv("alu_fp32")!;
    public static uint[]? GetShaderFP16(VulkanCapabilityProbe? probe = null) => _spvFp16 ??= LoadSpirv("alu_fp16");
    public static uint[]? GetShaderBF16(VulkanCapabilityProbe? probe = null) => _spvBf16 ??= LoadSpirv("alu_bf16");
    public static uint[]? GetShaderFP64(VulkanCapabilityProbe? probe = null) => _spvFp64 ??= LoadSpirv("alu_fp64");

    public static uint[] GetShaderINT32() => _spvInt32 ??= LoadSpirv("alu_int32")!;
    public static uint[]? GetShaderINT16(VulkanCapabilityProbe? probe = null) => _spvInt16 ??= LoadSpirv("alu_int16");
    public static uint[]? GetShaderINT8(VulkanCapabilityProbe? probe = null)  => _spvInt8  ??= LoadSpirv("alu_int8");
    public static uint[]? GetShaderINT64(VulkanCapabilityProbe? probe = null) => _spvInt64 ??= LoadSpirv("alu_int64");

    public static uint[]? GetShaderDP4A(VulkanCapabilityProbe? probe = null) => _spvDp4a ??= LoadSpirv("alu_dp4a");
    public static uint[]? GetShaderDP2A(VulkanCapabilityProbe? probe = null) => _spvDp2a ??= LoadSpirv("alu_dp2a");

    // Memory Hierarchy
    public static uint[] GetShaderL2Cache() => _spvMemL2 ??= LoadSpirv("mem_l2")!;
    public static uint[] GetShaderL3Cache() => _spvMemL3 ??= LoadSpirv("mem_l3")!;
    public static uint[] GetShaderMem()     => _spvMemVram ??= LoadSpirv("mem_bw")!;
    public static uint[] GetShaderLatency() => _spvPointerChase ??= LoadSpirv("mem_lat")!;

    // TMU Textures
    public static uint[] GetShaderTmuBilinear() => _spvTmuBdt   ??= LoadSpirv("tmu_bdt")!;
    public static uint[] GetShaderTmuAniso()    => _spvTmuAniso ??= LoadSpirv("tmu_aniso")!;
    public static uint[] GetShaderTmuBC()       => _spvTmuBc    ??= LoadSpirv("tmu_bc")!;
    public static uint[] GetShaderTmu3D()       => _spvTmu3D    ??= LoadSpirv("tmu_3d")!;

    // Matrix KHR
    public static uint[]? GetShaderMatFP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp16 != null) return _spvMatFp16;
        bool isIntel = probe?.VendorId == 0x8086;
        return _spvMatFp16 = LoadSpirv(isIntel ? "mat_khr_fp16_intel" : "mat_khr_fp16");
    }

    public static uint[]? GetShaderMatBF16(VulkanCapabilityProbe? probe = null)    => _spvMatBf16    ??= LoadSpirv("mat_khr_bf16");
    public static uint[]? GetShaderMatFP32(VulkanCapabilityProbe? probe = null)    => _spvMatFp32    ??= LoadSpirv("mat_khr_fp32");
    public static uint[]? GetShaderMatFP64(VulkanCapabilityProbe? probe = null)    => _spvMatFp64    ??= LoadSpirv("mat_khr_fp64");
    public static uint[]? GetShaderMatFP8_E4M3(VulkanCapabilityProbe? probe = null)=> _spvMatFp8E4M3 ??= LoadSpirv("mat_khr_fp8_e4m3");
    public static uint[]? GetShaderMatFP8_E5M2(VulkanCapabilityProbe? probe = null)=> _spvMatFp8E5M2 ??= LoadSpirv("mat_khr_fp8_e5m2");
    public static uint[]? GetShaderMatINT8(VulkanCapabilityProbe? probe = null)    => _spvMatInt8    ??= LoadSpirv("mat_khr_int8");
    public static uint[]? GetShaderMatINT4(VulkanCapabilityProbe? probe = null) => _spvMatInt4 ??= LoadSpirv("mat_khr_int4");
    public static uint[]? GetShaderMatINT16(VulkanCapabilityProbe? probe = null)   => _spvMatInt16   ??= LoadSpirv("mat_khr_int16");
    public static uint[]? GetShaderMatINT32(VulkanCapabilityProbe? probe = null)   => _spvMatInt32   ??= LoadSpirv("mat_khr_int32");
    public static uint[]? GetShaderMatINT64(VulkanCapabilityProbe? probe = null)   => _spvMatInt64   ??= LoadSpirv("mat_khr_int64");
}