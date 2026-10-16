[[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint8_t s = uint8_t(buffer[id.x]);
    uint8_t4 r0 = uint8_t4(s, s + 1, s + 2, s + 3);
    uint8_t4 r1 = r0 + 1; uint8_t4 r2 = r0 + 2; uint8_t4 r3 = r0 + 3;
    const uint8_t a = 5;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        r0 = r0 * a + r1;
        r1 = r1 * a + r2;
        r2 = r2 * a + r3;
        r3 = r3 * a + r0;
    }
    buffer[id.x] = uint(r0.x + r1.y + r2.z + r3.w);
}