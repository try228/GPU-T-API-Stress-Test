// Native/Vulkan/Shaders/RayTracing/rt_pipe_omm.slang
// RT Pipeline + Opacity Micromap Bitmask Evaluation on Hardware Traversal

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

    uint confirmedHits = 0;
    float accumulatedDist = 0.0f;

    [unroll]
    for (int r = 0; r < 32; r++)
    {
        float angle = seed + float(r) * 0.19634954f;
        RayDesc ray;
        ray.Origin = float3(cos(angle) * 0.3f, sin(angle) * 0.3f, -3.0f);
        ray.Direction = normalize(float3(sin(angle) * 0.15f, cos(angle) * 0.15f, 1.0f));
        ray.TMin = 0.001f;
        ray.TMax = 1000.0f;

        RayQuery<RAY_FLAG_NONE> q;
        q.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, ray);
        while (q.Proceed())
        {
            if (q.CandidateType() == CANDIDATE_NON_OPAQUE_TRIANGLE)
            {
                float2 bary = q.CandidateTriangleBarycentrics();
                int iu = clamp(int(bary.x * 4.0f), 0, 3);
                int iv = clamp(int(bary.y * 4.0f), 0, 3);
                uint ommMask = 0x55AAAA55;
                if (((ommMask >> ((iv * 4 + iu) * 2)) & 3) != 0)
                {
                    q.CommitNonOpaqueTriangleHit();
                }
            }
        }

        if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
        {
            confirmedHits++;
            accumulatedDist += q.CommittedRayT();
        }
    }

    g_output[tid] = uint4(confirmedHits, asuint(accumulatedDist), tid, 0x4F4D5050); // 'OMPP'
}