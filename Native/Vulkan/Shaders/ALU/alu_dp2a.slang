[[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;

[ForceInline]
int dp2a_hardware(int16_t2 a, int16_t2 b, int acc)
{
    int dotResult = spirv_asm {
        OpCapability DotProductKHR;
        OpCapability DotProductInputAllKHR;
        OpExtension "SPV_KHR_integer_dot_product";
        result:$$int = OpSDotKHR $a $b;
    };
    return dotResult + acc;
}

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    int s = buffer[id.x];
    int4 acc0 = int4(s, s + 1, s + 2, s + 3);

    int16_t2 a = int16_t2(int16_t(s ^ 0x0003), int16_t((s + 1) ^ 0x0005));
    int16_t2 b = int16_t2(int16_t(s ^ 0x0007), int16_t((s + 2) ^ 0x0009));

    [loop]
    for (int i = 128; i > 0; i--)
    {
        acc0.x = dp2a_hardware(a, b, acc0.y);
        acc0.y = dp2a_hardware(a, b, acc0.z);
        acc0.z = dp2a_hardware(a, b, acc0.w);
        acc0.w = dp2a_hardware(a, b, acc0.x);
    }

    buffer[id.x] = acc0.x + acc0.y + acc0.z + acc0.w;
}