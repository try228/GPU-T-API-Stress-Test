[[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferOut;
[[vk::binding(6, 0)]] Sampler3D tex3D;

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = bufferOut[id.x];
    float3 uvw = float3(float(id.x & 127) * 0.0078125f, 0.5f, s * 0.0001f);
    float4 acc = float4(0.0f);

    [loop]
    for (int i = 0; i < 64; i++)
    {
        acc += tex3D.SampleLevel(uvw + float3(0.002f * float(i), 0.0f, 0.001f * float(i)), 0.0f);
        acc += tex3D.SampleLevel(uvw + float3(0.0f, 0.002f * float(i), 0.002f * float(i)), 0.5f);
        acc += tex3D.SampleLevel(uvw - float3(0.002f * float(i), 0.001f * float(i), 0.0f), 1.0f);
        acc += tex3D.SampleLevel(uvw - float3(0.0f, 0.002f * float(i), 0.002f * float(i)), 0.0f);
    }
    bufferOut[id.x] = acc.x + acc.y + acc.z + acc.w;
}