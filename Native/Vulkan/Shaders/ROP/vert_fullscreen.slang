// Native/Vulkan/Shaders/Graphics/vert_fullscreen.slang
// Procedural Fullscreen Triangle Generator (Zero VBO Overhead)

struct VSOutput
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

[shader("vertex")]
VSOutput main(uint vid : SV_VertexID)
{
    VSOutput output;
    output.uv = float2((vid << 1) & 2, vid & 2);
    output.pos = float4(output.uv * 2.0f - 1.0f, 0.0f, 1.0f);
    return output;
}