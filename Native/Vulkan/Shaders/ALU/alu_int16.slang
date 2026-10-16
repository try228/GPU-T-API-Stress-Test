[[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    int16_t s = int16_t(buffer[id.x]);
    int16_t4 r0 = int16_t4(s, s + 1, s + 2, s + 3);
    int16_t4 r1 = r0 + 1; int16_t4 r2 = r0 + 2; int16_t4 r3 = r0 + 3;
    const int16_t a = 13;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        r0 = r0 * a + r1;
        r1 = r1 * a + r2;
        r2 = r2 * a + r3;
        r3 = r3 * a + r0;
    }
    buffer[id.x] = int(r0.x + r1.y + r2.z + r3.w);
}