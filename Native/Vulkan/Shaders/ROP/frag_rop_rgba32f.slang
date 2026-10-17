// Native/Vulkan/Shaders/Graphics/frag_rop_rgba32f.slang
// ROP RGBA32F Float Rate Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("fragment")]
float4 main(PSInput input) : SV_Target0
{
    return float4(input.uv.x * 100.0f, input.uv.y * 100.0f, 50.0f, 1.0f);
}