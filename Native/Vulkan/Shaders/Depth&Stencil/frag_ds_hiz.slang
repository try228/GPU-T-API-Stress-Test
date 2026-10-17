// Native/Vulkan/Shaders/Graphics/frag_ds_hiz.slang
// Hi-Z Tile Culling Engine Stress

struct PSInput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("fragment")]
float4 main(PSInput input, out float depth : SV_Depth) : SV_Target0
{
    uint2 pixel = uint2(input.pos.xy);
    depth = ((pixel.x ^ pixel.y) & 16) != 0 ? 0.2f : 0.8f;
    return float4(0.0f, input.uv.x, input.uv.y, 1.0f);
}