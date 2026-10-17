// Native/Vulkan/Shaders/Graphics/frag_rop_blend.slang
// ROP Alpha Blending Hardware Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("fragment")]
float4 main(PSInput input) : SV_Target0
{
    return float4(input.uv.x, input.uv.y, 0.5f, 0.5f);
}