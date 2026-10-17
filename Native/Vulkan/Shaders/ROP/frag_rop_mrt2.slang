// Native/Vulkan/Shaders/Graphics/frag_rop_mrt2.slang
// ROP MRT 2x Fillrate Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

struct PSOutputMRT2
{
    float4 target0 : SV_Target0;
    float4 target1 : SV_Target1;
};

[shader("fragment")]
PSOutputMRT2 main(PSInput input)
{
    PSOutputMRT2 o;
    o.target0 = float4(input.uv.x, input.uv.y, 0.5f, 1.0f);
    o.target1 = float4(1.0f - input.uv.x, 1.0f - input.uv.y, 0.5f, 1.0f);
    return o;
}