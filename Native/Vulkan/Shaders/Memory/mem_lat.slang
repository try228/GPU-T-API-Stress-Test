[[vk::binding(0, 0)]] RWStructuredBuffer<uint> buffer;
[numthreads(1, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint ptr = buffer[0];
    [loop]
    for (int k = 0; k < 256; k++)
    {
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
        ptr = buffer[ptr & 0x01FFFFFFu];
    }
    buffer[0] = ptr;
}