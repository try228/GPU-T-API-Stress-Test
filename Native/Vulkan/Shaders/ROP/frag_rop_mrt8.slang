// Native/Vulkan/Shaders/Graphics/frag_rop_mrt8.slang
// ROP MRT 8x Max Silicon Saturation Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

struct PSOutputMRT8
{
    float4 target0 : SV_Target0;
    float4 target1 : SV_Target1;
    float4 target2 : SV_Target2;
    float4 target3 : SV_Target3;
    float4 target4 : SV_Target4;
    float4 target5 : SV_Target5;
    float4 target6 : SV_Target6;
    float4 target7 : SV_Target7;
};

[shader("fragment")]
PSOutputMRT8 main(PSInput input)
{
    PSOutputMRT8 o;
    float x = input.uv.x, y = input.uv.y;
    o.target0 = float4(x, y, 0.1f, 1.0f);
    o.target1 = float4(y, x, 0.2f, 1.0f);
    o.target2 = float4(1.0f - x, y, 0.3f, 1.0f);
    o.target3 = float4(x, 1.0f - y, 0.4f, 1.0f);
    o.target4 = float4(1.0f - x, 1.0f - y, 0.5f, 1.0f);
    o.target5 = float4(0.6f, x, y, 1.0f);
    o.target6 = float4(x, 0.7f, y, 1.0f);
    o.target7 = float4(y, x, 0.8f, 1.0f);
    return o;
}