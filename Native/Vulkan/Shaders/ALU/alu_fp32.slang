[[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = buffer[id.x];
    float4 r0 = float4(s, s + 0.1f, s + 0.2f, s + 0.3f);
    float4 r1 = r0 + 0.1f; float4 r2 = r0 + 0.2f; float4 r3 = r0 + 0.3f;
    const float a0 = 1.000001f;
    const float a1 = 1.000005f;

    [loop]
    for (int i = 0; i < 64; i++)
    {
        r0 = fma(r0, a0, r1);
        r2 = fma(r2, a1, r3);
        r1 = fma(r1, a0, r0);
        r3 = fma(r3, a1, r2);

        r0 = fma(r0, a1, r2);
        r1 = fma(r1, a0, r3);
        r2 = fma(r2, a1, r0);
        r3 = fma(r3, a0, r1);
    }
    buffer[id.x] = r0.x + r1.y + r2.z + r3.w;
}