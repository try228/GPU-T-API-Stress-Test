[[vk::binding(0, 0)]] RWStructuredBuffer<float> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float s = buffer[id.x];
    vector<BFloat16, 2> a = vector<BFloat16, 2>(BFloat16(s), BFloat16(s + 0.1f));
    vector<BFloat16, 2> b = vector<BFloat16, 2>(BFloat16(1.0001f), BFloat16(1.0005f));
    float acc0 = s;
    float acc1 = s + 0.1f;
    float acc2 = s + 0.2f;
    float acc3 = s + 0.3f;

    [loop]
    for (int i = 0; i < 256; i++)
    {
        acc0 = acc0 + float(dot(a, b));
        acc1 = acc1 + float(dot(b, a));
        acc2 = acc2 + float(dot(a, b));
        acc3 = acc3 + float(dot(b, a));
    }
    buffer[id.x] = acc0 + acc1 + acc2 + acc3;
}