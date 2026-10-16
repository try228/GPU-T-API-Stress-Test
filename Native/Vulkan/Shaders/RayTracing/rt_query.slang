// Native/Vulkan/Shaders/RayTracing/rt_query.slang
// Hardware Ray Query (Direct RDNA 3 / RT Core Traversal) - SPIR-V 1.4

[[vk::binding(0, 0)]]
RWStructuredBuffer<uint4> g_output;

[[vk::binding(7, 0)]]
RaytracingAccelerationStructure g_tlas;

[shader("compute")]
[numthreads(64, 1, 1)]
void main(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint tid = dispatchThreadID.x;
    float seed = float(tid) * 0.00390625f;

    uint hitCount = 0;
    float accumulatedT = 0.0f;

    // 32 Hardware-accelerated rays per invocation directly traversing the BVH
    [unroll]
    for (int r = 0; r < 32; r++)
    {
        float angle = seed + float(r) * 0.19634954f;
        float cosA = cos(angle);
        float sinA = sin(angle);

        RayDesc ray;
        ray.Origin = float3(cosA * 0.3f, sinA * 0.3f, -3.0f);
        ray.Direction = normalize(float3(sinA * 0.15f, cosA * 0.15f, 1.0f));
        ray.TMin = 0.001f;
        ray.TMax = 1000.0f;

        // Directly invokes OpRayQueryInitializeKHR and OpRayQueryProceedKHR
        RayQuery<RAY_FLAG_NONE> q;
        q.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, ray);
        while (q.Proceed())
        {
            // Ray Accelerators execute BVH node fetches and triangle intersection in silicon
        }

        if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
        {
            hitCount++;
            accumulatedT += q.CommittedRayT();
        }
    }

    g_output[tid] = uint4(hitCount, asuint(accumulatedT), tid, 0x52545155); // 'RTQU'
}