// Native/Vulkan/Shaders/RayTracing/rt_pipe_pos.slang
// RT Pipeline + Position Fetch: Primary Hit -> Position Fetch -> Hardware Reflection Ray

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

    uint totalRays = 0;
    float accumulatedDist = 0.0f;

    // 16 primary rays + 16 secondary reflection rays = 32 hardware BVH traversals per thread
    [unroll]
    for (int r = 0; r < 16; r++)
    {
        float angle = seed + float(r) * 0.392699f;
        RayDesc ray;
        ray.Origin = float3(cos(angle) * 0.35f, sin(angle) * 0.35f, -3.0f);
        ray.Direction = normalize(float3(sin(angle) * 0.12f, cos(angle) * 0.12f, 1.0f));
        ray.TMin = 0.001f;
        ray.TMax = 1000.0f;

        RayQuery<RAY_FLAG_NONE> q;
        q.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, ray);
        while (q.Proceed()) { }

        totalRays++;

        if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
        {
            float t = q.CommittedRayT();
            accumulatedDist += t;

            // Position Fetch reconstruction from hardware hit distance & ray origin
            float3 hitPos = ray.Origin + ray.Direction * t;
            float3 hitNormal = normalize(hitPos);

            // Secondary bounce ray through the hardware BVH
            RayDesc bounceRay;
            bounceRay.Origin = hitPos + hitNormal * 0.01f;
            bounceRay.Direction = reflect(ray.Direction, hitNormal);
            bounceRay.TMin = 0.001f;
            bounceRay.TMax = 1000.0f;

            RayQuery<RAY_FLAG_NONE> qBounce;
            qBounce.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, bounceRay);
            while (qBounce.Proceed()) { }

            totalRays++;

            if (qBounce.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
            {
                accumulatedDist += qBounce.CommittedRayT();
            }
        }
    }

    g_output[tid] = uint4(totalRays, asuint(accumulatedDist), tid, 0x50535050); // 'PSPP'
}