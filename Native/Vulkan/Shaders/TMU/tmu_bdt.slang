[[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferOut;
[[vk::binding(3, 0)]] Sampler2D texSampler;

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = bufferOut[id.x];
    float2 uv = float2(float(id.x & 1023) * 0.0009765625f + s * 0.001f, 0.5f);
    float4 acc = float4(0.0f);

    [loop]
    for (int i = 0; i < 64; i++)
    {
        acc += texSampler.SampleLevel(uv + float2(0.0001f * float(i), 0.0002f * float(i)), 0.0f);
        acc += texSampler.SampleLevel(uv + float2(0.0003f * float(i), 0.0004f * float(i)), 0.5f);
        acc += texSampler.SampleLevel(uv + float2(0.0005f * float(i), 0.0006f * float(i)), 1.2f);
        acc += texSampler.SampleLevel(uv + float2(0.0007f * float(i), 0.0008f * float(i)), 0.0f);
    }
    bufferOut[id.x] = acc.x + acc.y + acc.z + acc.w;
}