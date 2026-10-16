[[vk::binding(0, 0)]] RWStructuredBuffer<int> buffer;

[ForceInline]
int dp4a_hardware(int a, int b, int acc)
{
    int dotResult = spirv_asm {
        OpCapability DotProductKHR;
        OpCapability DotProductInput4x8BitPackedKHR;
        OpExtension "SPV_KHR_integer_dot_product";
        result:$$int = OpSDotKHR $a $b 0;
    };
    return dotResult + acc;
}

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    int s = buffer[id.x];
    int4 acc0 = int4(s, s + 1, s + 2, s + 3);
    int4 acc1 = acc0 + 1;

    int a = s ^ 0x01020304;
    int b = (s + 1) ^ 0x05060708;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        acc0.x = dp4a_hardware(a, b, acc1.y);
        acc0.y = dp4a_hardware(a, b, acc1.z);
        acc1.x = dp4a_hardware(a, b, acc0.y);
        acc1.y = dp4a_hardware(a, b, acc0.x);
    }

    buffer[id.x] = acc0.x + acc0.y + acc1.x + acc1.y;
}