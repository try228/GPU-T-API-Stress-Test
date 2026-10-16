[[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferOut;
[[vk::binding(4, 0)]] Sampler2D texAniso;

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = bufferOut[id.x];
    float2 uv = float2(float(id.x & 1023) * 0.0009765625f, s * 0.0001f);
    
    float2 ddx = float2(1.0f / 1024.0f, 0.0f);
    float2 ddy = float2(0.0f, 16.0f / 1024.0f);
    float4 acc = float4(0.0f);

    [loop]
    for (int i = 0; i < 64; i++)
    {
        acc += texAniso.SampleGrad(uv + float2(0.0005f * float(i), 0.0f), ddx, ddy);
        acc += texAniso.SampleGrad(uv + float2(0.0f, 0.0005f * float(i)), ddy, ddx);
        acc += texAniso.SampleGrad(uv - float2(0.0003f * float(i), 0.0f), ddx, ddy);
        acc += texAniso.SampleGrad(uv - float2(0.0f, 0.0003f * float(i)), ddy, ddx);
    }
    bufferOut[id.x] = acc.x + acc.y + acc.z + acc.w;
}