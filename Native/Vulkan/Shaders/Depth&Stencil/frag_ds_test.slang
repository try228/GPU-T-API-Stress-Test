// Native/Vulkan/Shaders/Graphics/frag_ds_test.slang
// Depth / Stencil Standard Test & Write

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("fragment")]
float4 main(PSInput input, out float depth : SV_Depth) : SV_Target0
{
    depth = 0.5f;
    return float4(input.uv.x, input.uv.y, 0.0f, 1.0f);
}