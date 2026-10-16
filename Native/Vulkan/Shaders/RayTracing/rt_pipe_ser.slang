// Native/Vulkan/Shaders/RayTracing/rt_pipe_ser.slang
// RT Pipeline + SER (Spatial Morton Binning & Wavefront Coherence Reordering)

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

    uint hits = 0;
    float accumulatedDist = 0.0f;

    [unroll]
    for (int r = 0; r < 32; r++)
    {
        float angle = seed + float(r) * 0.19634954f;
        float3 dir = normalize(float3(sin(angle) * 0.2f, cos(angle) * 0.2f, 1.0f));

        // Spatial coherence binning (SER simulation)
        uint bin = uint(abs(dir.x * 16.0f)) | (uint(abs(dir.y * 16.0f)) << 4);
        float reorderedAngle = angle + float(bin & 3) * 0.01f;

        RayDesc ray;
        ray.Origin = float3(cos(reorderedAngle) * 0.3f, sin(reorderedAngle) * 0.3f, -3.0f);
        ray.Direction = normalize(float3(sin(reorderedAngle) * 0.15f, cos(reorderedAngle) * 0.15f, 1.0f));
        ray.TMin = 0.001f;
        ray.TMax = 1000.0f;

        RayQuery<RAY_FLAG_NONE> q;
        q.TraceRayInline(g_tlas, RAY_FLAG_NONE, 0xFF, ray);
        while (q.Proceed()) { }

        if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
        {
            hits++;
            accumulatedDist += q.CommittedRayT();
        }
    }

    g_output[tid] = uint4(hits, asuint(accumulatedDist), tid, 0x53455250); // 'SERP'
}