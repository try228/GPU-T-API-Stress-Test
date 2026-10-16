// Native/Vulkan/Shaders/RayTracing/rt_query_pos.slang
// Hardware Ray Query with Dynamic Position Fetch & Reflection Bounces - SPIR-V 1.4

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

    uint totalBounces = 0;
    float accumulatedDist = 0.0f;

    // 16 Dual-bounce primary + secondary reflection rays through the BVH
    [unroll]
    for (int r = 0; r < 16; r++)
    {
        float angle = seed + float(r) * 0.392699f;
        RayDesc ray;
        ray.Origin = float3(cos(angle) * 0.4f, sin(angle) * 0.4f, -3.0f);
        ray.Direction = normalize(float3(sin(angle) * 0.1f, cos(angle) * 0.1f, 1.0f));
        ray.TMin = 0.001f;
        ray.TMax = 1000.0f;

        RayQuery<RAY_FLAG_NONE> q;
        q.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, ray);
        while (q.Proceed()) { }

        if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
        {
            totalBounces++;
            float t = q.CommittedRayT();
            accumulatedDist += t;

            float2 bary = q.CommittedTriangleBarycentrics();
            float3 hitPos = ray.Origin + ray.Direction * t;
            float3 approxNorm = normalize(hitPos);

            // Cast secondary reflected ray into the hardware BVH
            RayDesc bounceRay;
            bounceRay.Origin = hitPos + approxNorm * 0.01f;
            bounceRay.Direction = reflect(ray.Direction, approxNorm);
            bounceRay.TMin = 0.001f;
            bounceRay.TMax = 1000.0f;

            RayQuery<RAY_FLAG_NONE> q2;
            q2.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, bounceRay);
            while (q2.Proceed()) { }

            if (q2.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
            {
                totalBounces++;
                accumulatedDist += q2.CommittedRayT();
            }
        }
    }

    g_output[tid] = uint4(totalBounces, asuint(accumulatedDist), tid, 0x504F5351); // 'POSQ'
}