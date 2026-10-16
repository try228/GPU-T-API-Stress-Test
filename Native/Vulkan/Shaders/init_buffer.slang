[[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint count = 67108864u;
    buffer[id.x] = (id.x + 1021u) % count;
}