namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Hardware compute shader generator targeting SPIR-V 1.0 (ALU, cache, and VRAM memory streaming)
/// and SPIR-V 1.3 (standard Khronos cooperative matrix operations).
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

    private static (int M, int N, int K) ResolveCoopMatDims(VulkanCapabilityProbe? probe, string type, int defM, int defN, int defK)
    {
        if (probe != null && probe.TryGetCoopMatDimensions(type, out int m, out int n, out int k))
            return (m, n, k);
        return (defM, defN, defK);
    }

    /// <summary>
    /// Generates SSBO initialization compute shader.
    /// </summary>
    public static uint[] GetShaderInitBuffer() => _spvInitGpu ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint count = 67108864u;
            buffer[id.x] = (id.x + 1021u) % count;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates FP32 compute shader optimized for dual-issue scalar execution.
    /// </summary>
    public static uint[] GetShaderFP32() => _spvFp32 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float s = buffer[id.x];
            float4 r0 = float4(s, s + 0.1f, s + 0.2f, s + 0.3f);
            float4 r1 = r0 + 0.1f; float4 r2 = r0 + 0.2f; float4 r3 = r0 + 0.3f;
            const float a0 = 1.000001f;
            const float a1 = 1.000005f;

            [loop]
            for (int i = 0; i < 64; i++)
            {
                r0 = fma(r0, a0, r1);
                r2 = fma(r2, a1, r3);
                r1 = fma(r1, a0, r0);
                r3 = fma(r3, a1, r2);

                r0 = fma(r0, a1, r2);
                r1 = fma(r1, a0, r3);
                r2 = fma(r2, a1, r0);
                r3 = fma(r3, a0, r1);
            }
            buffer[id.x] = r0.x + r1.y + r2.z + r3.w;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates packed 16-bit floating point compute shader.
    /// </summary>
    public static uint[] GetShaderFP16(VulkanCapabilityProbe? probe = null) => _spvFp16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            half s = half(buffer[id.x]);
            half4 r0 = half4(s, s + 0.1h, s + 0.2h, s + 0.3h);
            half4 r1 = r0 + 0.1h; half4 r2 = r0 + 0.2h; half4 r3 = r0 + 0.3h;
            const half a0 = 1.0001h;
            const half a1 = 1.0005h;

            [loop]
            for (int i = 0; i < 64; i++)
            {
                r0 = fma(r0, a0, r1);
                r2 = fma(r2, a1, r3);
                r1 = fma(r1, a0, r0);
                r3 = fma(r3, a1, r2);

                r0 = fma(r0, a1, r2);
                r1 = fma(r1, a0, r3);
                r2 = fma(r2, a1, r0);
                r3 = fma(r3, a0, r1);
            }
            buffer[id.x] = float(r0.x + r1.y + r2.z + r3.w);
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates true BFloat16 vector dot product compute shader via VK_KHR_shader_bfloat16.
    /// </summary>
    public static uint[] GetShaderBF16(VulkanCapabilityProbe? probe = null) => _spvBf16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            float s = buffer[id.x];
            vector<BFloat16, 2> a = vector<BFloat16, 2>(BFloat16(s), BFloat16(s + 0.1f));
            vector<BFloat16, 2> b = vector<BFloat16, 2>(BFloat16(1.0001f), BFloat16(1.0005f));
            float acc0 = s;
            float acc1 = s + 0.1f;
            float acc2 = s + 0.2f;
            float acc3 = s + 0.3f;

            [loop]
            for (int i = 0; i < 256; i++)
            {
                acc0 = acc0 + float(dot(a, b));
                acc1 = acc1 + float(dot(b, a));
                acc2 = acc2 + float(dot(a, b));
                acc3 = acc3 + float(dot(b, a));
            }
            buffer[id.x] = acc0 + acc1 + acc2 + acc3;
        }
        """, profile: "spirv_1_3");

    /// <summary>
    /// Generates double precision 64-bit floating point compute shader.
    /// </summary>
    public static uint[] GetShaderFP64(VulkanCapabilityProbe? probe = null) => _spvFp64 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<double> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            double s = buffer[id.x];
            double2 r0 = double2(s, s + 0.1); double2 r1 = r0 + 0.1;
            const double a = 1.00000001;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                r0 = fma(r0, a, r1);
                r1 = fma(r1, a, r0);
            }
            buffer[id.x] = r0.x + r1.y;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates 32-bit integer compute shader.
    /// </summary>
    public static uint[] GetShaderINT32() => _spvInt32 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint s = buffer[id.x];
            uint4 r0 = uint4(s, s + 1u, s + 2u, s + 3u);
            uint4 r1 = r0 + 1u; uint4 r2 = r0 + 2u; uint4 r3 = r0 + 3u;
            const uint a = 1664525u;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                r0 = r0 + r1 + a;
                r2 = r2 + r3 + a;
                r1 = r1 + r0 + a;
                r3 = r3 + r2 + a;
            }
            buffer[id.x] = r0.x + r1.y + r2.z + r3.w;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates 16-bit integer compute shader.
    /// </summary>
    public static uint[] GetShaderINT16(VulkanCapabilityProbe? probe = null) => _spvInt16 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int16_t s = int16_t(buffer[id.x]);
            int16_t4 r0 = int16_t4(s, s + 1, s + 2, s + 3);
            int16_t4 r1 = r0 + 1; int16_t4 r2 = r0 + 2; int16_t4 r3 = r0 + 3;
            const int16_t a = 13;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                r0 = r0 * a + r1;
                r1 = r1 * a + r2;
                r2 = r2 * a + r3;
                r3 = r3 * a + r0;
            }
            buffer[id.x] = int(r0.x + r1.y + r2.z + r3.w);
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates 8-bit integer compute shader.
    /// </summary>
    public static uint[] GetShaderINT8(VulkanCapabilityProbe? probe = null) => _spvInt8 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint8_t s = uint8_t(buffer[id.x]);
            uint8_t4 r0 = uint8_t4(s, s + 1, s + 2, s + 3);
            uint8_t4 r1 = r0 + 1; uint8_t4 r2 = r0 + 2; uint8_t4 r3 = r0 + 3;
            const uint8_t a = 5;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                r0 = r0 * a + r1;
                r1 = r1 * a + r2;
                r2 = r2 * a + r3;
                r3 = r3 * a + r0;
            }
            buffer[id.x] = uint(r0.x + r1.y + r2.z + r3.w);
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates 64-bit integer compute shader.
    /// </summary>
    public static uint[] GetShaderINT64(VulkanCapabilityProbe? probe = null) => _spvInt64 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint64_t> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint64_t s = buffer[id.x];
            uint64_t r0 = s; uint64_t r1 = s + 1UL;
            const uint64_t a = 6364136223846793005UL;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                r0 = r0 * a + r1;
                r1 = r1 * a + r0;
            }
            buffer[id.x] = r0 + r1;
        }
        """, profile: "spirv_1_0");

   /// <summary>
    /// Generates hardware INT8 DP4A packed dot product compute shader targeting native v_dot4_i32_iu8 on RDNA 3.
    /// Hardened against NIR/ACO constant folding via dynamic input dependency.
    /// </summary>
    public static uint[] GetShaderDP4A(VulkanCapabilityProbe? probe = null) => _spvDp4a ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;

        [ForceInline]
        int dp4a_hardware(int a, int b, int acc)
        {
            int dotResult = spirv_asm {
                OpCapability DotProductKHR;
                OpCapability DotProductInput4x8BitPackedKHR;
                OpExtension "SPV_KHR_integer_dot_product";
                result:$$int = OpSDotKHR $a $b 0;
            };
            return dotResult + acc;
        }

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int s = buffer[id.x];
            int4 acc0 = int4(s, s + 1, s + 2, s + 3);
            int4 acc1 = acc0 + 1;

            int a = s ^ 0x01020304;
            int b = (s + 1) ^ 0x05060708;

            [loop]
            for (int i = 128; i > 0; i--)
            {
                acc0.x = dp4a_hardware(a, b, acc1.y);
                acc0.y = dp4a_hardware(a, b, acc1.z);
                acc1.x = dp4a_hardware(a, b, acc0.y);
                acc1.y = dp4a_hardware(a, b, acc0.x);
            }

            buffer[id.x] = acc0.x + acc0.y + acc1.x + acc1.y;
        }
        """, profile: "spirv_1_3");

    /// <summary>
    /// Generates hardware INT16 DP2A dot product compute shader targeting native v_dot2_i32_i16 on RDNA 2 and Intel Arc.
    /// Operates on 16-bit vector pairs via OpSDotKHR and protected against constant folding.
    /// </summary>
    public static uint[] GetShaderDP2A(VulkanCapabilityProbe? probe = null) => _spvDp2a ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;

        [ForceInline]
        int dp2a_hardware(int16_t2 a, int16_t2 b, int acc)
        {
            int dotResult = spirv_asm {
                OpCapability DotProductKHR;
                OpCapability DotProductInputAllKHR;
                OpExtension "SPV_KHR_integer_dot_product";
                result:$$int = OpSDotKHR $a $b;
            };
            return dotResult + acc;
        }

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            int s = buffer[id.x];
            int4 acc0 = int4(s, s + 1, s + 2, s + 3);

            int16_t2 a = int16_t2(int16_t(s ^ 0x0003), int16_t((s + 1) ^ 0x0005));
            int16_t2 b = int16_t2(int16_t(s ^ 0x0007), int16_t((s + 2) ^ 0x0009));

            [loop]
            for (int i = 128; i > 0; i--)
            {
                acc0.x = dp2a_hardware(a, b, acc0.y);
                acc0.y = dp2a_hardware(a, b, acc0.z);
                acc0.z = dp2a_hardware(a, b, acc0.w);
                acc0.w = dp2a_hardware(a, b, acc0.x);
            }

            buffer[id.x] = acc0.x + acc0.y + acc0.z + acc0.w;
        }
        """, profile: "spirv_1_3");

    // =========================================================================
    // Standard KHR Cooperative Matrix Shaders (VK_KHR_cooperative_matrix)
    // =========================================================================

    public static uint[] GetShaderMatFP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp16 != null) return _spvMatFp16;
        var (m, n, k) = ResolveCoopMatDims(probe, "FP16", 16, 16, 16);

        return _spvMatFp16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<half> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<half> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<half> bufferC;

        typealias MatA = linalg::CoopMat<half, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<half, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<half, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0h);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatBF16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatBf16 != null) return _spvMatBf16;
        var (m, n, k) = ResolveCoopMatDims(probe, "BF16", 16, 16, 16);

        return _spvMatBf16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<BFloat16> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<BFloat16> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<BFloat16, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<BFloat16, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatINT8(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatInt8 != null) return _spvMatInt8;
        var (m, n, k) = ResolveCoopMatDims(probe, "INT8", 16, 16, 16);

        return _spvMatInt8 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int8_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int8_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int8_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int8_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatFP32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp32 != null) return _spvMatFp32;
        var (m, n, k) = ResolveCoopMatDims(probe, "FP32", 16, 16, 16);

        return _spvMatFp32 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<float> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<float, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatFP64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp64 != null) return _spvMatFp64;
        var (m, n, k) = ResolveCoopMatDims(probe, "FP64", 8, 8, 4);

        return _spvMatFp64 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<double> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<double> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<double> bufferC;

        typealias MatA = linalg::CoopMat<double, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<double, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<double, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatFP8_E4M3(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp8E4M3 != null) return _spvMatFp8E4M3;
        var (m, n, k) = ResolveCoopMatDims(probe, "FP8_E4M3", 16, 16, 32);

        return _spvMatFp8E4M3 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE4M3> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE4M3> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE4M3, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE4M3, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatFP8_E5M2(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatFp8E5M2 != null) return _spvMatFp8E5M2;
        var (m, n, k) = ResolveCoopMatDims(probe, "FP8_E5M2", 16, 16, 32);

        return _spvMatFp8E5M2 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE5M2> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE5M2> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE5M2, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE5M2, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatINT4(VulkanCapabilityProbe? probe = null) => _spvMatInt4 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<uint> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int4_t, MemoryScope.Subgroup, 8, 64, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int4_t, MemoryScope.Subgroup, 64, 8, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, 8, 8, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm {
                OpCapability Int4TypeINTEL;
                OpCapability Int4CooperativeMatrixINTEL;
                OpExtension "SPV_INTEL_int4";
                result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c;
            };
        }

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * 8 * 8) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, 64);
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, 8);
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMma(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, 8);
        }
        """, profile: "spirv_1_3");

    public static uint[] GetShaderMatINT16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatInt16 != null) return _spvMatInt16;
        var (m, n, k) = ResolveCoopMatDims(probe, "INT16", 16, 16, 16);

        return _spvMatInt16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int16_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int16_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int16_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int16_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatINT32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatInt32 != null) return _spvMatInt32;
        var (m, n, k) = ResolveCoopMatDims(probe, "INT32", 16, 16, 16);

        return _spvMatInt32 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderMatINT64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvMatInt64 != null) return _spvMatInt64;
        var (m, n, k) = ResolveCoopMatDims(probe, "INT64", 8, 8, 4);

        return _spvMatInt64 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int64_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int64_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int64_t> bufferC;

        typealias MatA = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMma(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint sgIdx = id.x / WaveGetLaneCount();
            uint outOffset = (sgIdx * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0L);

            [loop]
            for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    // =========================================================================
    // Cache & Physical VRAM Bus Streaming
    // =========================================================================

    /// <summary>
    /// Generates L1/L2 cache saturation compute shader.
    /// Operates on a safe 1 MB footprint (0x0000FFFF mask) that strictly resides within L2 across all architectures (including 2 MB L2 on GTX 1080).
    /// </summary>
    public static uint[] GetShaderL2Cache() => _spvMemL2 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint4> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint idx = id.x & 0x0000FFFFu;
            uint4 v = buffer[idx];

            [loop]
            for (int i = 0; i < 32; i++)
            {
                v += buffer[(idx + i) & 0x0000FFFFu];
            }
            buffer[idx] = v;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Generates L3 / AMD Infinity Cache compute shader.
    /// Operates on a 32 MB footprint (0x001FFFFF mask) residing inside Infinity Cache while exhausting L2.
    /// </summary>
    public static uint[] GetShaderL3Cache() => _spvMemL3 ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint4> buffer;
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint idx = id.x & 0x001FFFFFu;
            uint4 v = buffer[idx];

            [loop]
            for (int i = 0; i < 16; i++)
            {
                v += buffer[(idx + (i * 1024)) & 0x001FFFFFu];
            }
            buffer[idx] = v;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Coalesced streaming compute shader saturating physical GDDR/HBM bus bandwidth.
    /// Each thread streams 8 consecutive 128-bit vectors (128 bytes read, 128 bytes written).
    /// </summary>
    public static uint[] GetShaderMem() => _spvMemVram ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint4> bufferIn;
        [[vk::binding(1, 0)]] RWStructuredBuffer<uint4> bufferOut;

        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint baseIdx = id.x * 8u;

            uint4 a0 = bufferIn[baseIdx + 0u];
            uint4 a1 = bufferIn[baseIdx + 1u];
            uint4 a2 = bufferIn[baseIdx + 2u];
            uint4 a3 = bufferIn[baseIdx + 3u];
            uint4 a4 = bufferIn[baseIdx + 4u];
            uint4 a5 = bufferIn[baseIdx + 5u];
            uint4 a6 = bufferIn[baseIdx + 6u];
            uint4 a7 = bufferIn[baseIdx + 7u];

            bufferOut[baseIdx + 0u] = a0 + 1u;
            bufferOut[baseIdx + 1u] = a1 + 1u;
            bufferOut[baseIdx + 2u] = a2 + 1u;
            bufferOut[baseIdx + 3u] = a3 + 1u;
            bufferOut[baseIdx + 4u] = a4 + 1u;
            bufferOut[baseIdx + 5u] = a5 + 1u;
            bufferOut[baseIdx + 6u] = a6 + 1u;
            bufferOut[baseIdx + 7u] = a7 + 1u;
        }
        """, profile: "spirv_1_0");

    /// <summary>
    /// Pointer-chasing latency compute shader measuring round-trip memory access latency.
    /// </summary>
    public static uint[] GetShaderLatency() => _spvPointerChase ??= SlangCompiler.CompileToSpirv("""
        [[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
        [numthreads(1, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint ptr = buffer[0];
            [loop]
            for (int k = 0; k < 256; k++)
            {
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
                ptr = buffer[ptr & 0x01FFFFFFu];
            }
            buffer[0] = ptr;
        }
        """, profile: "spirv_1_0");
}