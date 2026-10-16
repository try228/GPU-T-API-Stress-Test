// Native/Vulkan/Shaders/RayTracing/rt_pipe.slang
// Hardware Ray Tracing Pipeline Simulation: Primary Ray + Shadow Ray per Invocation

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

    // 16 primary rays + 16 shadow rays through the hardware BVH
    [unroll]
    for (int r = 0; r < 16; r++)
    {
        float angle = seed + float(r) * 0.392699f;
        RayDesc ray;
        ray.Origin = float3(cos(angle) * 0.3f, sin(angle) * 0.3f, -3.0f);
        ray.Direction = normalize(float3(sin(angle) * 0.15f, cos(angle) * 0.15f, 1.0f));
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

            // Shadow ray toward procedural light source
            RayDesc shadowRay;
            shadowRay.Origin = ray.Origin + ray.Direction * t + float3(0.0f, 0.01f, 0.0f);
            shadowRay.Direction = normalize(float3(0.5f, 1.0f, -0.5f));
            shadowRay.TMin = 0.001f;
            shadowRay.TMax = 100.0f;

            RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> qShadow;
            qShadow.TraceRayInline(g_tlas, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH, 0xFF, shadowRay);
            while (qShadow.Proceed()) { }

            totalRays++;
        }
    }

    g_output[tid] = uint4(totalRays, asuint(accumulatedDist), tid, 0x52545050); // 'RTPP'
}