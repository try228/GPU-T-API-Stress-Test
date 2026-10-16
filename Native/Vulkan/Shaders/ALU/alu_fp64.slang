[[vk::binding(0, 0)]] RWStructuredBuffer<double> buffer;
[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    double s = buffer[id.x];
    double2 r0 = double2(s, s + 0.1); double2 r1 = r0 + 0.1;
    const double a = 1.00000001;

    [loop]
    for (int i = 128; i > 0; i--)
    {
        r0 = fma(r0, a, r1);
        r1 = fma(r1, a, r0);
    }
    buffer[id.x] = r0.x + r1.y;
}