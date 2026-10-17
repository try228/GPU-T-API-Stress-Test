// Native/Vulkan/Shaders/Graphics/frag_rop_mrt4.slang
// ROP MRT 4x G-Buffer Fillrate Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

struct PSOutputMRT4
{
    float4 target0 : SV_Target0;
    float4 target1 : SV_Target1;
    float4 target2 : SV_Target2;
    float4 target3 : SV_Target3;
};

[shader("fragment")]
PSOutputMRT4 main(PSInput input)
{
    PSOutputMRT4 o;
    o.target0 = float4(input.uv.x, input.uv.y, 0.2f, 1.0f);
    o.target1 = float4(input.uv.y, input.uv.x, 0.4f, 1.0f);
    o.target2 = float4(1.0f - input.uv.x, 0.6f, input.uv.y, 1.0f);
    o.target3 = float4(0.8f, input.uv.x, 1.0f - input.uv.y, 1.0f);
    return o;
}