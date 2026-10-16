[[vk::binding(0, 0)]] RWStructuredBuffer<uint4> bufferIn;
[[vk::binding(1, 0)]] RWStructuredBuffer<uint4> bufferOut;

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint baseIdx = id.x * 8u;

    uint4 a0 = bufferIn[baseIdx + 0u];
    uint4 a1 = bufferIn[baseIdx + 1u];
    uint4 a2 = bufferIn[baseIdx + 2u];
    uint4 a3 = bufferIn[baseIdx + 3u];
    uint4 a4 = bufferIn[baseIdx + 4u];
    uint4 a5 = bufferIn[baseIdx + 5u];
    uint4 a6 = bufferIn[baseIdx + 6u];
    uint4 a7 = bufferIn[baseIdx + 7u];

    bufferOut[baseIdx + 0u] = a0 + 1u;
    bufferOut[baseIdx + 1u] = a1 + 1u;
    bufferOut[baseIdx + 2u] = a2 + 1u;
    bufferOut[baseIdx + 3u] = a3 + 1u;
    bufferOut[baseIdx + 4u] = a4 + 1u;
    bufferOut[baseIdx + 5u] = a5 + 1u;
    bufferOut[baseIdx + 6u] = a6 + 1u;
    bufferOut[baseIdx + 7u] = a7 + 1u;
}