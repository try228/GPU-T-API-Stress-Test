// Native/Vulkan/Shaders/Graphics/frag_ds_earlyz.slang
// Early-Z Culling Hardware Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[earlydepthstencil]
[shader("fragment")]
float4 main(PSInput input) : SV_Target0
{
    return float4(1.0f, 0.0f, 0.0f, 1.0f);
}