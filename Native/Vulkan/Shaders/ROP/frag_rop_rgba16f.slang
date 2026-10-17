// Native/Vulkan/Shaders/Graphics/frag_rop_rgba16f.slang
// ROP RGBA16F HDR Rate Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("fragment")]
float4 main(PSInput input) : SV_Target0
{
    return float4(input.uv.x * 2.0f, input.uv.y * 2.0f, 1.5f, 1.0f);
}