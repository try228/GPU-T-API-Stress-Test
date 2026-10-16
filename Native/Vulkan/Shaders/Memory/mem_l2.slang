[[vk::binding(0, 0)]] RWStructuredBuffer<uint4> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint idx = id.x & 0x0000FFFFu;
    uint4 v = buffer[idx];

    [loop]
    for (int i = 0; i < 32; i++)
    {
        v += buffer[(idx + i) & 0x0000FFFFu];
    }
    buffer[idx] = v;
}