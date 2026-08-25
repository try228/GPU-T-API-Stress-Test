namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Pre-compiled SPIR-V compute shader binaries for Vulkan stress testing.
/// </summary>
public static class VulkanShaders
{
    /// <summary>
    /// Valid SPIR-V 1.0 Compute Shader (LocalSize: 64x1x1, SSBO Binding 0).
    /// Verified with Khronos spirv-val.
    /// </summary>
    public static readonly uint[] StressComputeSpirV = new uint[]
    {
        0x07230203, 0x00010000, 0x00080001, 0x00000019, 0x00000000,
        0x00020011, 0x00000001, // OpCapability Shader
        0x0003000e, 0x00000000, 0x00000001, // OpMemoryModel Logical GLSL450
        0x0006000f, 0x00000005, 0x00000004, 0x6e69616d, 0x00000000, 0x00000009, // OpEntryPoint GLCompute %4 "main" %9
        0x00060010, 0x00000004, 0x00000011, 0x00000040, 0x00000001, 0x00000001, // OpExecutionMode %4 LocalSize 64 1 1
        0x00040047, 0x00000009, 0x0000000b, 0x0000001c, // OpDecorate %9 BuiltIn GlobalInvocationId
        0x00040047, 0x0000000b, 0x00000006, 0x00000004, // OpDecorate %11 ArrayStride 4
        0x00050048, 0x0000000c, 0x00000000, 0x00000023, 0x00000000, // OpMemberDecorate %12 0 Offset 0
        0x00030047, 0x0000000c, 0x00000003, // OpDecorate %12 BufferBlock
        0x00040047, 0x0000000e, 0x00000022, 0x00000000, // OpDecorate %14 DescriptorSet 0
        0x00040047, 0x0000000e, 0x00000021, 0x00000000, // OpDecorate %14 Binding 0
        0x00020013, 0x00000002, // %2 = OpTypeVoid
        0x00030021, 0x00000003, 0x00000002, // %3 = OpTypeFunction %2
        0x00040015, 0x00000006, 0x00000020, 0x00000000, // %6 = OpTypeInt 32 0 (uint)
        0x00040017, 0x00000007, 0x00000006, 0x00000003, // %7 = OpTypeVector %6 3
        0x00040020, 0x00000008, 0x00000001, 0x00000007, // %8 = OpTypePointer Input %7
        0x0004003b, 0x00000008, 0x00000009, 0x00000001, // %9 = OpVariable %8 Input (gl_GlobalInvocationID)
        0x0003001d, 0x0000000b, 0x00000006, // %11 = OpTypeRuntimeArray %6
        0x0003001e, 0x0000000c, 0x0000000b, // %12 = OpTypeStruct %11
        0x00040020, 0x0000000d, 0x00000002, 0x0000000c, // %13 = OpTypePointer Uniform %12
        0x0004003b, 0x0000000d, 0x0000000e, 0x00000002, // %14 = OpVariable %13 Uniform
        0x00040015, 0x0000000f, 0x00000020, 0x00000001, // %15 = OpTypeInt 32 1 (int)
        0x0004002b, 0x0000000f, 0x00000010, 0x00000000, // %16 = OpConstant %15 0
        0x0004002b, 0x00000006, 0x00000011, 0x00000001, // %17 = OpConstant %6 1
        0x00040020, 0x00000012, 0x00000001, 0x00000006, // %18 = OpTypePointer Input %6
        0x00040020, 0x00000013, 0x00000002, 0x00000006, // %19 = OpTypePointer Uniform %6
        0x00050036, 0x00000002, 0x00000004, 0x00000000, 0x00000003, // OpFunction
        0x000200f8, 0x00000005, // OpLabel
        0x00050041, 0x00000012, 0x00000014, 0x00000009, 0x00000010, // OpAccessChain %18 %9 %16
        0x0004003d, 0x00000006, 0x00000015, 0x00000014, // OpLoad
        0x00060041, 0x00000013, 0x00000016, 0x0000000e, 0x00000010, 0x00000015, // OpAccessChain %19 %14 %16 %21
        0x0004003d, 0x00000006, 0x00000017, 0x00000016, // OpLoad
        0x00050080, 0x00000006, 0x00000018, 0x00000017, 0x00000011, // OpIAdd
        0x0003003e, 0x00000016, 0x00000018, // OpStore
        0x000100fd, // OpReturn
        0x00010038  // OpFunctionEnd
    };
}