namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Dedicated compute shader generator for NVIDIA Matrix architectures:
/// VK_NV_cooperative_matrix (NV1 - Subgroup Scope via native GLSL) and
/// VK_NV_cooperative_matrix2 (NV2 - Workgroup Scope via Slang).
/// </summary>
public static class VulkanNvMatrixShaders
{
    /// <summary>Standard compute workgroup thread count.</summary>
    public const uint LocalSizeX = 64;

    // Bytecode caches for VK_NV_cooperative_matrix (NV1 - GLSL)
    private static uint[]? _spvNv1Fp16;
    private static uint[]? _spvNv1Fp32;
    private static uint[]? _spvNv1Fp64;
    private static uint[]? _spvNv1Int8;
    private static uint[]? _spvNv1Int16;
    private static uint[]? _spvNv1Int32;
    private static uint[]? _spvNv1Int64;

    // Bytecode caches for VK_NV_cooperative_matrix2 (NV2 - Slang)
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
    // SECTION 1: VK_NV_cooperative_matrix (NV1 - Native GLSL)
    // =========================================================================

    /// <summary>
    /// Generates NV1 FP16 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_FP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp16 != null) return _spvNv1Fp16;
        var (m, n, k) = ResolveDims(probe, "FP16", 16, 16, 16);

        return _spvNv1Fp16 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_float16 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { float16_t a[]; };
        layout(set = 0, binding = 1) buffer BufferB { float16_t b[]; };
        layout(set = 0, binding = 2) buffer BufferC { float16_t c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            fcoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            fcoopmatNV<16, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            fcoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = fcoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{n}}>(float16_t(0.0));

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 FP32 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_FP32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp32 != null) return _spvNv1Fp32;
        var (m, n, k) = ResolveDims(probe, "FP32", 16, 16, 16);

        return _spvNv1Fp32 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { float a[]; };
        layout(set = 0, binding = 1) buffer BufferB { float b[]; };
        layout(set = 0, binding = 2) buffer BufferC { float c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            fcoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            fcoopmatNV<32, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            fcoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = fcoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{n}}>(0.0);

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 FP64 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_FP64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Fp64 != null) return _spvNv1Fp64;
        var (m, n, k) = ResolveDims(probe, "FP64", 8, 8, 4);

        return _spvNv1Fp64 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_float64 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { float64_t a[]; };
        layout(set = 0, binding = 1) buffer BufferB { float64_t b[]; };
        layout(set = 0, binding = 2) buffer BufferC { float64_t c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            fcoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            fcoopmatNV<64, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            fcoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = fcoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{n}}>(0.0);

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 INT8 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_INT8(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int8 != null) return _spvNv1Int8;
        var (m, n, k) = ResolveDims(probe, "INT8", 16, 16, 16);

        return _spvNv1Int8 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_int8 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { int8_t a[]; };
        layout(set = 0, binding = 1) buffer BufferB { int8_t b[]; };
        layout(set = 0, binding = 2) buffer BufferC { int8_t c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            icoopmatNV<8, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            icoopmatNV<8, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            icoopmatNV<8, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = icoopmatNV<8, gl_ScopeSubgroup, {{m}}, {{n}}>(int8_t(0));

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 INT16 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_INT16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int16 != null) return _spvNv1Int16;
        var (m, n, k) = ResolveDims(probe, "INT16", 16, 16, 16);

        return _spvNv1Int16 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_int16 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { int16_t a[]; };
        layout(set = 0, binding = 1) buffer BufferB { int16_t b[]; };
        layout(set = 0, binding = 2) buffer BufferC { int16_t c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            icoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            icoopmatNV<16, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            icoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = icoopmatNV<16, gl_ScopeSubgroup, {{m}}, {{n}}>(int16_t(0));

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 INT32 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_INT32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int32 != null) return _spvNv1Int32;
        var (m, n, k) = ResolveDims(probe, "INT32", 16, 16, 16);

        return _spvNv1Int32 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_int32 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { int a[]; };
        layout(set = 0, binding = 1) buffer BufferB { int b[]; };
        layout(set = 0, binding = 2) buffer BufferC { int c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            icoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            icoopmatNV<32, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            icoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = icoopmatNV<32, gl_ScopeSubgroup, {{m}}, {{n}}>(0);

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    /// <summary>
    /// Generates NV1 INT64 cooperative matrix compute shader via GL_NV_cooperative_matrix.
    /// </summary>
    public static uint[] GetShaderNV1_INT64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv1Int64 != null) return _spvNv1Int64;
        var (m, n, k) = ResolveDims(probe, "INT64", 8, 8, 4);

        return _spvNv1Int64 = GlslCompiler.CompileToSpirv($$"""
        #version 450 core
        #extension GL_NV_cooperative_matrix : enable
        #extension GL_EXT_shader_explicit_arithmetic_types_int64 : enable

        layout(local_size_x = {{LocalSizeX}}, local_size_y = 1, local_size_z = 1) in;

        layout(set = 0, binding = 0) buffer BufferA { int64_t a[]; };
        layout(set = 0, binding = 1) buffer BufferB { int64_t b[]; };
        layout(set = 0, binding = 2) buffer BufferC { int64_t c[]; };

        void main()
        {
            uint sgIdx = gl_GlobalInvocationID.x / gl_SubgroupSize;
            uint outOffset = (sgIdx * {{m}}u * {{n}}u) & 0x0001FFFFu;

            icoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{k}}> matA;
            icoopmatNV<64, gl_ScopeSubgroup, {{k}}, {{n}}> matB;
            icoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{n}}> acc;

            coopMatLoadNV(matA, a, 0u, {{k}}u, false);
            coopMatLoadNV(matB, b, 0u, {{n}}u, false);
            acc = icoopmatNV<64, gl_ScopeSubgroup, {{m}}, {{n}}>(0L);

            for (int i = 0; i < 16; i++)
            {
                acc = coopMatMulAddNV(matA, matB, acc);
            }

            coopMatStoreNV(acc, c, outOffset, {{n}}u, false);
        }
        """);
    }

    // =========================================================================
    // SECTION 2: VK_NV_cooperative_matrix2 (NV2 - Slang)
    // =========================================================================

    public static uint[] GetShaderNV2_FP16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp16 != null) return _spvNv2Fp16;
        var (m, n, k) = ResolveDims(probe, "FP16", 16, 16, 16);

        return _spvNv2Fp16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<half> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<half> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<half, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<half, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_BF16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Bf16 != null) return _spvNv2Bf16;
        var (m, n, k) = ResolveDims(probe, "BF16", 16, 16, 16);

        return _spvNv2Bf16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<BFloat16> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<BFloat16> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<BFloat16, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<BFloat16, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_FP8_E4M3(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp8E4M3 != null) return _spvNv2Fp8E4M3;
        var (m, n, k) = ResolveDims(probe, "FP8_E4M3", 16, 16, 32);

        return _spvNv2Fp8E4M3 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE4M3> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE4M3> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE4M3, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE4M3, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_FP8_E5M2(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp8E5M2 != null) return _spvNv2Fp8E5M2;
        var (m, n, k) = ResolveDims(probe, "FP8_E5M2", 16, 16, 32);

        return _spvNv2Fp8E5M2 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<FloatE5M2> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<FloatE5M2> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<FloatE5M2, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<FloatE5M2, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_FP32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp32 != null) return _spvNv2Fp32;
        var (m, n, k) = ResolveDims(probe, "FP32", 16, 16, 16);

        return _spvNv2Fp32 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<float> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

        typealias MatA = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<float, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_FP64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Fp64 != null) return _spvNv2Fp64;
        var (m, n, k) = ResolveDims(probe, "FP64", 8, 8, 4);

        return _spvNv2Fp64 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<double> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<double> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<double> bufferC;

        typealias MatA = linalg::CoopMat<double, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<double, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<double, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_INT8(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int8 != null) return _spvNv2Int8;
        var (m, n, k) = ResolveDims(probe, "INT8", 16, 16, 16);

        return _spvNv2Int8 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int8_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int8_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int8_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int8_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_INT16(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int16 != null) return _spvNv2Int16;
        var (m, n, k) = ResolveDims(probe, "INT16", 16, 16, 16);

        return _spvNv2Int16 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int16_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int16_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int16_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int16_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_INT32(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int32 != null) return _spvNv2Int32;
        var (m, n, k) = ResolveDims(probe, "INT32", 16, 16, 16);

        return _spvNv2Int32 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int> bufferC;

        typealias MatA = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
            }

            acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, {{n}});
        }
        """, profile: "spirv_1_3");
    }

    public static uint[] GetShaderNV2_INT64(VulkanCapabilityProbe? probe = null)
    {
        if (_spvNv2Int64 != null) return _spvNv2Int64;
        var (m, n, k) = ResolveDims(probe, "INT64", 8, 8, 4);

        return _spvNv2Int64 = SlangCompiler.CompileToSpirv($$"""
        [[vk::binding(0, 0)]] RWStructuredBuffer<int64_t> bufferA;
        [[vk::binding(1, 0)]] RWStructuredBuffer<int64_t> bufferB;
        [[vk::binding(2, 0)]] RWStructuredBuffer<int64_t> bufferC;

        typealias MatA = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{m}}, {{k}}, linalg::CoopMatMatrixUse.MatrixA>;
        typealias MatB = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{k}}, {{n}}, linalg::CoopMatMatrixUse.MatrixB>;
        typealias MatC = linalg::CoopMat<int64_t, MemoryScope.Workgroup, {{m}}, {{n}}, linalg::CoopMatMatrixUse.MatrixAccumulator>;

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
                acc = coopMatMulAdd(matA, matB, acc);
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