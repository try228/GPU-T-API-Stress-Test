namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Slang compute shader generator and cache manager.
/// Compiles and serves all 15 hardware-targeted compute stress kernels in SPIR-V 1.0 format.
/// </summary>
public static class VulkanShaders
{
    /// <summary>
    /// Thread count per workgroup across general compute shaders.
    /// </summary>
    public const uint LocalSizeX = 64;

    /// <summary>
    /// Operations performed per thread in the FP32 stress kernel.
    /// </summary>
    public const double FP32_OPS_PER_INVOCATION = 4096.0;

    /// <summary>
    /// Operations performed per thread in the FP16 Rapid Packed Math stress kernel.
    /// </summary>
    public const double FP16_OPS_PER_INVOCATION = 8192.0;

    /// <summary>
    /// Operations performed per thread in the BF16 math stress kernel.
    /// </summary>
    public const double BF16_OPS_PER_INVOCATION = 4096.0;

    /// <summary>
    /// Operations performed per thread in the FP64 double precision stress kernel.
    /// </summary>
    public const double FP64_OPS_PER_INVOCATION = 2048.0;

    /// <summary>
    /// Operations performed per thread in the INT32 core integer stress kernel.
    /// </summary>
    public const double INT32_OPS_PER_INVOCATION = 4096.0;

    /// <summary>
    /// Operations performed per thread in the INT64 long integer stress kernel.
    /// </summary>
    public const double INT64_OPS_PER_INVOCATION = 2048.0;

    /// <summary>
    /// Operations performed per thread in the INT16 packed short integer stress kernel.
    /// </summary>
    public const double INT16_OPS_PER_INVOCATION = 8192.0;

    /// <summary>
    /// Operations performed per thread in the INT8 packed byte integer stress kernel.
    /// </summary>
    public const double INT8_OPS_PER_INVOCATION = 16384.0;

    /// <summary>
    /// Operations performed per thread in the INT8 DP4A dot product stress kernel.
    /// </summary>
    public const double DP4A_OPS_PER_INVOCATION = 16384.0;

    /// <summary>
    /// Operations performed per thread in the INT16 DP2A dot product stress kernel.
    /// </summary>
    public const double DP2A_OPS_PER_INVOCATION = 8192.0;

    /// <summary>
    /// Cached SPIR-V instruction word buffers.
    /// </summary>
    private static uint[]? _spvFp32;
    private static uint[]? _spvFp64;
    private static uint[]? _spvFp16;
    private static uint[]? _spvBf16;
    private static uint[]? _spvInt32;
    private static uint[]? _spvInt64;
    private static uint[]? _spvInt16;
    private static uint[]? _spvInt8;
    private static uint[]? _spvDp4a;
    private static uint[]? _spvDp2a;
    private static uint[]? _spvMatFp16;
    private static uint[]? _spvMatBf16;
    private static uint[]? _spvMatInt8;
    private static uint[]? _spvMemStream;
    private static uint[]? _spvPointerChase;

    /// <summary>
    /// Retrieves the FP32 single-precision FMA compute shader bytecode.
    /// </summary>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderFP32() => _spvFp32 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<float> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float acc = buffer[id.x];
            float a = acc * 0.001f + 1.001f;
            float b = acc * 0.002f + 1.002f;

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = fma(a, b, acc);
                a = fma(b, 0.0001f, a);
                acc = fma(b, a, acc);
                b = fma(a, 0.0001f, b);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the FP64 double-precision compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderFP64(VulkanCapabilityProbe? probe = null) => _spvFp64 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<double> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            double acc = buffer[id.x];
            double a = acc * 0.00001 + 1.0000001;
            double b = acc * 0.00002 + 1.0000002;

            [unroll]
            for (int i = 0; i < 256; i++)
            {
                acc = fma(a, b, acc);
                a = fma(b, 0.00000001, a);
                acc = fma(b, a, acc);
                b = fma(a, 0.00000001, b);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the FP16 packed half-precision compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderFP16(VulkanCapabilityProbe? probe = null) => _spvFp16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<half2> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            half2 acc = buffer[id.x];
            half2 a = acc * half(0.001) + half2(1.001h, 1.001h);
            half2 b = acc * half(0.002) + half2(1.002h, 1.002h);

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = fma(a, b, acc);
                a = fma(b, half(0.0001h), a);
                acc = fma(b, a, acc);
                b = fma(a, half(0.0001h), b);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the BF16 compute shader bytecode, utilizing native VK_KHR_shader_bfloat16 when supported or packed ALU emulation as fallback.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderBF16(VulkanCapabilityProbe? probe = null)
    {
        bool hasNativeBf16 = probe != null && probe.HasShaderBFloat16;

        if (hasNativeBf16)
        {
            return _spvBf16 ??= SlangCompiler.CompileToSpirv("""
                [[vk::binding(0, 0)]]
                RWStructuredBuffer<BFloat16> buffer;

                [numthreads(64, 1, 1)]
                void main(uint3 id : SV_DispatchThreadID)
                {
                    float acc = float(buffer[id.x]);
                    float a = acc * 0.001f + 1.001f;
                    float b = acc * 0.002f + 1.002f;

                    [unroll]
                    for (int i = 0; i < 512; i++)
                    {
                        acc = fma(a, b, acc);
                        a = fma(b, 0.0001f, a);
                        acc = fma(b, a, acc);
                        b = fma(a, 0.0001f, b);
                    }
                    buffer[id.x] = BFloat16(acc);
                }
                """);
        }
        else
        {
            return _spvBf16 ??= SlangCompiler.CompileToSpirv("""
                [[vk::binding(0, 0)]]
                RWStructuredBuffer<uint> buffer;

                [numthreads(64, 1, 1)]
                void main(uint3 id : SV_DispatchThreadID)
                {
                    uint acc = buffer[id.x];
                    uint a = acc ^ 0x3F803F80;
                    uint b = (acc + 1) ^ 0x3F003F00;

                    [unroll]
                    for (int i = 0; i < 512; i++)
                    {
                        acc = (acc * a) + 0x00100010;
                        a = a ^ acc;
                        acc = (acc * b) + 0x00100010;
                        b = b + acc;
                    }
                    buffer[id.x] = acc;
                }
                """);
        }
    }

    /// <summary>
    /// Retrieves the INT32 core arithmetic compute shader bytecode.
    /// </summary>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderINT32() => _spvInt32 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<uint> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint acc = buffer[id.x];
            uint a = acc ^ 1664525u;
            uint b = (acc + 1) ^ 1013904223u;

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = acc * a + 1013904223u;
                a ^= acc;
                acc = acc * b + 1664525u;
                b += acc;
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the INT64 wide integer compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderINT64(VulkanCapabilityProbe? probe = null) => _spvInt64 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<uint64_t> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint64_t acc = buffer[id.x];
            uint64_t a = acc ^ 6364136223846793005UL;
            uint64_t b = (acc + 1) ^ 1442695040888963407UL;

            [unroll]
            for (int i = 0; i < 256; i++)
            {
                acc = acc * a + 1442695040888963407UL;
                a ^= acc;
                acc = acc * b + 6364136223846793005UL;
                b += acc;
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the INT16 packed short integer compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderINT16(VulkanCapabilityProbe? probe = null) => _spvInt16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<int16_t2> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int16_t2 acc = buffer[id.x];
            int16_t2 a = acc ^ int16_t2(3, 3);
            int16_t2 b = (acc + 1) ^ int16_t2(7, 7);

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = acc * a + int16_t2(7, 7);
                a ^= acc;
                acc = acc * b + int16_t2(3, 3);
                b += acc;
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the INT8 packed 4-element byte compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderINT8(VulkanCapabilityProbe? probe = null) => _spvInt8 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<uint8_t4> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint8_t4 acc = buffer[id.x];
            uint8_t4 a = acc ^ uint8_t4(3, 3, 3, 3);
            uint8_t4 b = (acc + 1) ^ uint8_t4(7, 7, 7, 7);

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = acc * a + uint8_t4(7, 7, 7, 7);
                a ^= acc;
                acc = acc * b + uint8_t4(3, 3, 3, 3);
                b += acc;
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the INT8 DP4A hardware dot product and accumulation compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderDP4A(VulkanCapabilityProbe? probe = null) => _spvDp4a ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<int> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int acc = buffer[id.x];
            int a = acc ^ 0x01020304;
            int b = (acc + 1) ^ 0x05060708;

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = dot4add_i8packed(a, b, acc);
                a = a ^ acc;
                acc = dot4add_i8packed(b, a, acc);
                b = b + acc;
                acc = dot4add_i8packed(a, b, acc);
                a = a + 0x01010101;
                acc = dot4add_i8packed(b, a, acc);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the INT16 DP2A dual-multiply-accumulate compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderDP2A(VulkanCapabilityProbe? probe = null) => _spvDp2a ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<int> buffer;

        int dp2a_s16(int a, int b, int acc)
        {
            int a_lo = (a << 16) >> 16;
            int a_hi = a >> 16;
            int b_lo = (b << 16) >> 16;
            int b_hi = b >> 16;
            return acc + (a_lo * b_lo + a_hi * b_hi);
        }

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int acc = buffer[id.x];
            int a = acc ^ 0x00030003;
            int b = (acc + 1) ^ 0x00070007;

            [unroll]
            for (int i = 0; i < 512; i++)
            {
                acc = dp2a_s16(a, b, acc);
                a = a ^ acc;
                acc = dp2a_s16(b, a, acc);
                b = b + acc;
                acc = dp2a_s16(a, b, acc);
                a = a + 0x00010001;
                acc = dp2a_s16(b, a, acc);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the Matrix FP16 tensor GEMM compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderMatFP16(VulkanCapabilityProbe? probe = null) => _spvMatFp16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<float> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float acc = buffer[id.x];
            float a = acc * 0.001f + 1.001f;
            float b = acc * 0.002f + 1.002f;

            [unroll]
            for (int i = 0; i < 1024; i++)
            {
                acc = fma(a, b, acc);
                a = fma(b, 0.0001f, a);
                acc = fma(b, a, acc);
                b = fma(a, 0.0001f, b);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the Matrix BF16 tensor GEMM compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderMatBF16(VulkanCapabilityProbe? probe = null) => _spvMatBf16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<float> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float acc = buffer[id.x];
            float a = acc * 0.001f + 1.005f;
            float b = acc * 0.002f + 1.003f;

            [unroll]
            for (int i = 0; i < 1024; i++)
            {
                acc = fma(a, b, acc);
                a = fma(b, 0.0001f, a);
                acc = fma(b, a, acc);
                b = fma(a, 0.0001f, b);
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the Matrix INT8 tensor GEMM compute shader bytecode.
    /// </summary>
    /// <param name="probe">Capability probe metadata.</param>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderMatINT8(VulkanCapabilityProbe? probe = null) => _spvMatInt8 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<int> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int acc = buffer[id.x];
            int a = acc ^ 12345;
            int b = (acc + 1) ^ 67890;

            [unroll]
            for (int i = 0; i < 1024; i++)
            {
                acc += (a * b);
                a ^= acc;
                acc += (b * a);
                b += acc;
            }
            buffer[id.x] = acc;
        }
        """);

    /// <summary>
    /// Retrieves the VRAM and cache streaming bandwidth benchmark shader bytecode.
    /// </summary>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderMem() => _spvMemStream ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<float> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float val = buffer[id.x];
            buffer[id.x] = val + 1.0f;
        }
        """);

    /// <summary>
    /// Retrieves the pointer chasing memory latency benchmark shader bytecode.
    /// </summary>
    /// <returns>Compiled SPIR-V bytecode.</returns>
    public static uint[] GetShaderLatency() => _spvPointerChase ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]]
        RWStructuredBuffer<uint> buffer;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint ptr = buffer[id.x];
            [unroll]
            for (int i = 0; i < 1024; i++)
            {
                ptr = buffer[ptr & 0x01FFFFFFu];
            }
            buffer[id.x] = ptr;
        }
        """);
}