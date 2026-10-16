[[vk::binding(0, 0)]] RWStructuredBuffer<uint4> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint idx = id.x & 0x001FFFFFu;
    uint4 v = buffer[idx];

    [loop]
    for (int i = 0; i < 16; i++)
    {
        v += buffer[(idx + (i * 1024)) & 0x001FFFFFu];
    }
    buffer[idx] = v;
}