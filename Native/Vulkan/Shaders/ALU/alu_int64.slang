[[vk::binding(0, 0)]] RWStructuredBuffer<uint64_t> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint64_t s = buffer[id.x];
    uint64_t r0 = s; uint64_t r1 = s + 1UL;
    const uint64_t a = 6364136223846793005UL;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        r0 = r0 * a + r1;
        r1 = r1 * a + r0;
    }
    buffer[id.x] = r0 + r1;
}