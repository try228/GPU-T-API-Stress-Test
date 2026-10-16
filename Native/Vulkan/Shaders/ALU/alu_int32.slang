[[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint s = buffer[id.x];
    uint4 r0 = uint4(s, s + 1u, s + 2u, s + 3u);
    uint4 r1 = r0 + 1u; uint4 r2 = r0 + 2u; uint4 r3 = r0 + 3u;
    const uint a = 1664525u;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        r0 = r0 + r1 + a;
        r2 = r2 + r3 + a;
        r1 = r1 + r0 + a;
        r3 = r3 + r2 + a;
    }
    buffer[id.x] = r0.x + r1.y + r2.z + r3.w;
}