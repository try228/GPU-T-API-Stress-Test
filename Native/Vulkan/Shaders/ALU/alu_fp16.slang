[[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    half s = half(buffer[id.x]);
    half4 r0 = half4(s, s + 0.1h, s + 0.2h, s + 0.3h);
    half4 r1 = r0 + 0.1h; half4 r2 = r0 + 0.2h; half4 r3 = r0 + 0.3h;
    const half a0 = 1.0001h;
    const half a1 = 1.0005h;

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
    buffer[id.x] = float(r0.x + r1.y + r2.z + r3.w);
}