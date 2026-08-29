namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Pre-compiled SPIR-V 1.0 compute shader binaries for honest GPU stress testing.
/// </summary>
public static class VulkanShaders
{
    /// <summary>
    /// Number of local invocations per workgroup (X-dimension).
    /// </summary>
    public const uint LocalSizeX = 64;

    /// <summary>
    /// Number of floating-point operations (FLOPs) performed per invocation.
    /// 512 loop iterations * 8 FLOPs (4 dual-issue FMAs per iteration) = 4096 FLOPs.
    /// </summary>
    public const ulong FlopsPerInvocation = 4096;

    /// <summary>
    /// Valid SPIR-V 1.0 Compute Shader (LocalSize: 64x1x1, SSBO Binding 0).
    /// Contains a structured loop with FP32 Fused Multiply-Add (FMA) arithmetic.
    /// Conforms strictly to Vulkan 1.0 and Khronos SPIR-V 1.0 specifications.
    /// </summary>
    public static readonly uint[] StressComputeSpirV = new uint[]
    {
        // Header: Magic, Version 1.0, Generator, Bound (47), Schema (0)
        0x07230203, 0x00010000, 0x00080001, 0x0000002F, 0x00000000,

        // OpCapability Shader (Opcode 17, 2 words)
        0x00020011, 0x00000001,
        // OpMemoryModel Logical GLSL450 (Opcode 14, 3 words)
        0x0003000E, 0x00000000, 0x00000001,
        // OpEntryPoint GLCompute %23 "main" %10 (Opcode 15, 6 words)
        0x0006000F, 0x00000005, 0x00000017, 0x6E69616D, 0x00000000, 0x0000000A,
        // OpExecutionMode %23 LocalSize 64 1 1 (Opcode 16, 6 words)
        0x00060010, 0x00000017, 0x00000011, 0x00000040, 0x00000001, 0x00000001,

        // Decorations (Opcode 71, 72)
        0x00040047, 0x0000000A, 0x0000000B, 0x0000001C, // OpDecorate %10 BuiltIn GlobalInvocationId
        0x00040047, 0x0000000B, 0x00000006, 0x00000004, // OpDecorate %11 ArrayStride 4
        0x00050048, 0x0000000C, 0x00000000, 0x00000023, 0x00000000, // OpMemberDecorate %12 0 Offset 0
        0x00030047, 0x0000000C, 0x00000003,             // OpDecorate %12 BufferBlock
        0x00040047, 0x0000000E, 0x00000022, 0x00000000, // OpDecorate %14 DescriptorSet 0
        0x00040047, 0x0000000E, 0x00000021, 0x00000000, // OpDecorate %14 Binding 0

        // Type Declarations
        0x00020013, 0x00000002,                         // %2 = OpTypeVoid (Opcode 19)
        0x00030021, 0x00000003, 0x00000002,             // %3 = OpTypeFunction %2 (Opcode 33)
        0x00040015, 0x00000004, 0x00000020, 0x00000001, // %4 = OpTypeInt 32 1 (signed int) (Opcode 21)
        0x00040015, 0x00000005, 0x00000020, 0x00000000, // %5 = OpTypeInt 32 0 (unsigned int) (Opcode 21)
        0x00030016, 0x00000006, 0x00000020,             // %6 = OpTypeFloat 32 (Opcode 22)
        0x00020014, 0x00000007,                         // %7 = OpTypeBool (Opcode 20)
        0x00040017, 0x00000008, 0x00000005, 0x00000003, // %8 = OpTypeVector %5 3 (Opcode 23)
        0x00040020, 0x00000009, 0x00000001, 0x00000008, // %9 = OpTypePointer Input %8 (Opcode 32)
        0x0004003B, 0x00000009, 0x0000000A, 0x00000001, // %10 = OpVariable %9 Input (Opcode 59)
        0x0003001D, 0x0000000B, 0x00000006,             // %11 = OpTypeRuntimeArray %6 (Opcode 29)
        0x0003001E, 0x0000000C, 0x0000000B,             // %12 = OpTypeStruct %11 (Opcode 30)
        0x00040020, 0x0000000D, 0x00000002, 0x0000000C, // %13 = OpTypePointer Uniform %12 (Opcode 32)
        0x0004003B, 0x0000000D, 0x0000000E, 0x00000002, // %14 = OpVariable %13 Uniform (Opcode 59)
        0x00040020, 0x0000000F, 0x00000001, 0x00000005, // %15 = OpTypePointer Input %5 (Opcode 32)
        0x00040020, 0x00000010, 0x00000002, 0x00000006, // %16 = OpTypePointer Uniform %6 (Opcode 32)

        // Constants (Opcode 43)
        0x0004002B, 0x00000004, 0x00000011, 0x00000000, // %17 = OpConstant %4 0 (int 0)
        0x0004002B, 0x00000004, 0x00000012, 0x00000001, // %18 = OpConstant %4 1 (int 1)
        0x0004002B, 0x00000004, 0x00000013, 0x00000200, // %19 = OpConstant %4 512 (loop count)
        0x0004002B, 0x00000005, 0x00000014, 0x00000000, // %20 = OpConstant %5 0 (uint 0)
        0x0004002B, 0x00000006, 0x00000015, 0x3F800347, // %21 = OpConstant %6 1.0001f
        0x0004002B, 0x00000006, 0x00000016, 0x36D1B717, // %22 = OpConstant %6 0.0001f

        // Function %23 "main" (Opcode 54)
        0x00050036, 0x00000002, 0x00000017, 0x00000000, 0x00000003,

        // Block %24: Entry (OpLabel = 248 = 0xF8)
        0x000200F8, 0x00000018,
        0x00050041, 0x0000000F, 0x00000019, 0x0000000A, 0x00000014, // %25 = OpAccessChain %15 %10 %20 (Opcode 65)
        0x0004003D, 0x00000005, 0x0000001A, 0x00000019,             // %26 = OpLoad %5 %25 (gid) (Opcode 61)
        0x00060041, 0x00000010, 0x0000001B, 0x0000000E, 0x00000011, 0x0000001A, // %27 = OpAccessChain %16 %14 %17 %26
        0x0004003D, 0x00000006, 0x0000001C, 0x0000001B,             // %28 = OpLoad %6 %27 (init_val)
        0x000200F9, 0x0000001D,                                     // OpBranch %29 (Opcode 249 = 0xF9)

        // Block %29: Loop Header (OpLabel = 248 = 0xF8)
        0x000200F8, 0x0000001D,
        0x000700F5, 0x00000004, 0x0000001E, 0x00000011, 0x00000018, 0x0000002C, 0x0000002B, // %30 = OpPhi %4 %17 %24 %44 %43 (i) (Opcode 245 = 0xF5, 7 words)
        0x000700F5, 0x00000006, 0x0000001F, 0x0000001C, 0x00000018, 0x00000028, 0x0000002B, // %31 = OpPhi %6 %28 %24 %40 %43 (acc0) (Opcode 245 = 0xF5, 7 words)
        0x000700F5, 0x00000006, 0x00000020, 0x0000001C, 0x00000018, 0x0000002A, 0x0000002B, // %32 = OpPhi %6 %28 %24 %42 %43 (acc1) (Opcode 245 = 0xF5, 7 words)
        0x000500B1, 0x00000007, 0x00000021, 0x0000001E, 0x00000013, // %33 = OpSLessThan %7 %30 %19 (Opcode 177 = 0xB1, 5 words)
        0x000400F6, 0x0000002D, 0x0000002B, 0x00000000,             // OpLoopMerge %45 %43 None (Opcode 246 = 0xF6, 4 words)
        0x000400FA, 0x00000021, 0x00000022, 0x0000002D,             // OpBranchConditional %33 %34 %45 (Opcode 250 = 0xFA, 4 words)

        // Block %34: Loop Body (Heavy FP32 FMAs)
        0x000200F8, 0x00000022,
        0x00050085, 0x00000006, 0x00000023, 0x0000001F, 0x00000015, // %35 = OpFMul %6 %31 %21 (Opcode 133 = 0x85, 5 words)
        0x00050081, 0x00000006, 0x00000024, 0x00000023, 0x00000016, // %36 = OpFAdd %6 %35 %22 (Opcode 129 = 0x81, 5 words)
        0x00050085, 0x00000006, 0x00000025, 0x00000020, 0x00000015, // %37 = OpFMul %6 %32 %21
        0x00050081, 0x00000006, 0x00000026, 0x00000025, 0x00000024, // %38 = OpFAdd %6 %37 %36
        0x00050085, 0x00000006, 0x00000027, 0x00000024, 0x00000015, // %39 = OpFMul %6 %36 %21
        0x00050081, 0x00000006, 0x00000028, 0x00000027, 0x00000026, // %40 = OpFAdd %6 %39 %38
        0x00050085, 0x00000006, 0x00000029, 0x00000026, 0x00000015, // %41 = OpFMul %6 %38 %21
        0x00050081, 0x00000006, 0x0000002A, 0x00000029, 0x00000028, // %42 = OpFAdd %6 %41 %40
        0x000200F9, 0x0000002B,                                     // OpBranch %43 (Opcode 249 = 0xF9)

        // Block %43: Continue Target
        0x000200F8, 0x0000002B,
        0x00050080, 0x00000004, 0x0000002C, 0x0000001E, 0x00000012, // %44 = OpIAdd %4 %30 %18 (i++) (Opcode 128 = 0x80, 5 words)
        0x000200F9, 0x0000001D,                                     // OpBranch %29 (Opcode 249 = 0xF9)

        // Block %45: Merge Target
        0x000200F8, 0x0000002D,
        0x00050081, 0x00000006, 0x0000002E, 0x0000001F, 0x00000020, // %46 = OpFAdd %6 %31 %32 (Opcode 129 = 0x81, 5 words)
        0x0003003E, 0x0000001B, 0x0000002E,                         // OpStore %27 %46 (Opcode 62 = 0x3E, 3 words)
        0x000100FD,                                                 // OpReturn (Opcode 253 = 0xFD, 1 word)
        0x00010038                                                  // OpFunctionEnd (Opcode 56 = 0x38, 1 word)
    };
}