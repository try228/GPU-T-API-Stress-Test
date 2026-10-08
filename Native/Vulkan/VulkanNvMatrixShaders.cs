namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Hardware compute shader generator targeting NVIDIA Cooperative Matrix architectures:
/// VK_NV_cooperative_matrix (NV1 - Subgroup Scope) and VK_NV_cooperative_matrix2 (NV2 - Workgroup Scope).
/// </summary>
public static class VulkanNvMatrixShaders
{
    /// <summary>Compute workgroup thread count.</summary>
    public const uint LocalSizeX = 64;

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

    private static (int M, int N, int K) ResolveDims(VulkanCapabilityProbe? probe, string type, int defM, int defN, int defK)
    {
        if (probe != null && probe.TryGetCoopMatDimensions(type, out int m, out int n, out int k))
            return (m, n, k);
        return (defM, defN, defK);
    }

    // =========================================================================
    // SECTION 1: VK_NV_cooperative_matrix (NV1 - Subgroup Scope)
    // =========================================================================

    /// <summary>
    /// Generates NV1 FP16 cooperative matrix compute shader with FP32 accumulator.
    /// </summary>
    public static uint[] GetShaderNV1_FP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp16 != null) return _spvNv1Fp16;
        var (m, n, k) = ResolveDims(probe, "FP16", 16, 16, 16);

        return _spvNv1Fp16 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<half> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<half> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<half, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<half, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 FP32 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_FP32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp32 != null) return _spvNv1Fp32;
        var (m, n, k) = ResolveDims(probe, "FP32", 16, 16, 16);

        return _spvNv1Fp32 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<float> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<float, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 FP64 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_FP64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp64 != null) return _spvNv1Fp64;
        var (m, n, k) = ResolveDims(probe, "FP64", 8, 8, 4);

        return _spvNv1Fp64 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<double> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<double> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<double> bufferC;

        typealias MatA = linalg::CoopMat<double, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<double, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<double, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 INT8 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_INT8(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int8 != null) return _spvNv1Int8;
        var (m, n, k) = ResolveDims(probe, "INT8", 16, 16, 16);

        return _spvNv1Int8 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int8_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int8_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int8_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int8_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 INT16 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_INT16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int16 != null) return _spvNv1Int16;
        var (m, n, k) = ResolveDims(probe, "INT16", 16, 16, 16);

        return _spvNv1Int16 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int16_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int16_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int16_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int16_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 INT32 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_INT32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int32 != null) return _spvNv1Int32;
        var (m, n, k) = ResolveDims(probe, "INT32", 16, 16, 16);

        return _spvNv1Int32 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV1 INT64 cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV1_INT64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int64 != null) return _spvNv1Int64;
        var (m, n, k) = ResolveDims(probe, "INT64", 8, 8, 4);

        return _spvNv1Int64 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix"
        #pragma spirv capability CooperativeMatrixNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int64_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int64_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int64_t> bufferC;

        typealias MatA = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int64_t, MemoryScope.Subgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddNV $a $b $c; };
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
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    // =========================================================================
    // SECTION 2: VK_NV_cooperative_matrix2 (NV2 - Workgroup Scope)
    // =========================================================================

    /// <summary>
    /// Generates NV2 FP16 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_FP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp16 != null) return _spvNv2Fp16;
        var (m, n, k) = ResolveDims(probe, "FP16", 16, 16, 16);

        return _spvNv2Fp16 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<half> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<half> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<half, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<half, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 BFloat16 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_BF16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Bf16 != null) return _spvNv2Bf16;
        var (m, n, k) = ResolveDims(probe, "BF16", 16, 16, 16);

        return _spvNv2Bf16 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<BFloat16> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<BFloat16> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<BFloat16, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<BFloat16, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 FP8 (E4M3) workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_FP8_E4M3(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp8E4M3 != null) return _spvNv2Fp8E4M3;
        var (m, n, k) = ResolveDims(probe, "FP8_E4M3", 16, 16, 32);

        return _spvNv2Fp8E4M3 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE4M3> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE4M3> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE4M3, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE4M3, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 FP8 (E5M2) workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_FP8_E5M2(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp8E5M2 != null) return _spvNv2Fp8E5M2;
        var (m, n, k) = ResolveDims(probe, "FP8_E5M2", 16, 16, 32);

        return _spvNv2Fp8E5M2 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE5M2> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE5M2> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE5M2, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE5M2, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 FP32 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_FP32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp32 != null) return _spvNv2Fp32;
        var (m, n, k) = ResolveDims(probe, "FP32", 16, 16, 16);

        return _spvNv2Fp32 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<float> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<float, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0f);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 FP64 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_FP64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp64 != null) return _spvNv2Fp64;
        var (m, n, k) = ResolveDims(probe, "FP64", 8, 8, 4);

        return _spvNv2Fp64 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<double> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<double> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<double> bufferC;

        typealias MatA = linalg::CoopMat<double, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<double, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<double, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0.0);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 INT8 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_INT8(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int8 != null) return _spvNv2Int8;
        var (m, n, k) = ResolveDims(probe, "INT8", 16, 16, 16);

        return _spvNv2Int8 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int8_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int8_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int8_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int8_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 INT16 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_INT16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int16 != null) return _spvNv2Int16;
        var (m, n, k) = ResolveDims(probe, "INT16", 16, 16, 16);

        return _spvNv2Int16 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int16_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int16_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int16_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int16_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 INT32 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_INT32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int32 != null) return _spvNv2Int32;
        var (m, n, k) = ResolveDims(probe, "INT32", 16, 16, 16);

        return _spvNv2Int32 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Generates NV2 INT64 workgroup-scoped cooperative matrix compute shader.
    /// </summary>
    public static uint[] GetShaderNV2_INT64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int64 != null) return _spvNv2Int64;
        var (m, n, k) = ResolveDims(probe, "INT64", 8, 8, 4);

        return _spvNv2Int64 = SlangCompiler.CompileToSpirv($$"""
        #pragma spirv extension "SPV_NV_cooperative_matrix2"
        #pragma spirv capability CooperativeMatrixWorkgroupScopeNV

        [[vk::binding(0, 0)]] RWStructuredBuffer<int64_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int64_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int64_t> bufferC;

        typealias MatA = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

        [ForceInline]
        MatC coopMmaNV2(MatA a, MatB b, MatC c)
        {
            return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
        }

        [numthreads({{LocalSizeX}}, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
        {
            uint outOffset = (groupId.x * {{m}} * {{n}}) & 0x0001FFFFu;

            MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, {{k}});
            MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, {{n}});
            MatC acc;
            acc.fill(0L);

            [loop]
            for (int i = 0; i < 16; i++)
            {
                acc = coopMmaNV2(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    /// <summary>
    /// Resets all cached SPIR-V bytecode modules across NV1 and NV2 pipelines.
    /// </summary>
    public static void ResetCache()
    {
        _spvNv1Fp16 = null; _spvNv1Fp32 = null; _spvNv1Fp64 = null;
        _spvNv1Int8 = null; _spvNv1Int16 = null; _spvNv1Int32 = null; _spvNv1Int64 = null;

        _spvNv2Fp16 = null; _spvNv2Bf16 = null; _spvNv2Fp8E4M3 = null; _spvNv2Fp8E5M2 = null;
        _spvNv2Fp32 = null; _spvNv2Fp64 = null;
        _spvNv2Int8 = null; _spvNv2Int16 = null; _spvNv2Int32 = null; _spvNv2Int64 = null;
    }
}