[[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferOut;
[[vk::binding(5, 0)]] Sampler2D texBC;

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = bufferOut[id.x];
    float2 uv = float2(float(id.x & 1023) * 0.0009765625f + s * 0.0001f, 0.25f);
    float4 acc = float4(0.0f);

    [loop]
    for (int i = 0; i < 64; i++)
    {
        acc += texBC.SampleLevel(uv + float2(0.001f * float(i), 0.0f), 0.0f);
        acc += texBC.SampleLevel(uv + float2(0.0f, 0.001f * float(i)), 1.0f);
        acc += texBC.SampleLevel(uv - float2(0.001f * float(i), 0.0f), 0.0f);
        acc += texBC.SampleLevel(uv - float2(0.0f, 0.001f * float(i)), 2.0f);
    }
    bufferOut[id.x] = acc.x + acc.y + acc.z + acc.w;
}