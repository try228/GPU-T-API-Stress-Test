namespace GPU_T.StressTest.Core;

/// <summary>
/// Global application configuration and kill switches.
/// Follows GPU-T master kill switch architecture (see GPU-T Services/AppConfig.cs).
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// Master killswitch for experimental and unverified backends.
    /// When set to false, only backends marked as production-ready will run.
    /// </summary>
    public const bool EnableExperimentalBackends = false;

    // ==========================================
    // BACKEND FEATURE FLAGS / KILL SWITCHES
    // ==========================================

    /// <summary>
    /// Vulkan 1.0/1.3 compute, matrix, raytracing, and raster graphics pipelines.
    /// Production-ready and active for GPU-T main integration.
    /// </summary>
    public const bool EnableVulkanSupport = true;

    /// <summary>
    /// Direct3D 12 (VKD3D / VKD3D-Proton) backend.
    /// Currently in active development (Milestone 2).
    /// </summary>
    public const bool EnableDx12Support = false;

    /// <summary>
    /// Desktop OpenGL 3.3 Core & OpenGL ES 3.0 (including Mesa Zink) backends.
    /// </summary>
    public const bool EnableOpenGlSupport = false;

    /// <summary>
    /// Direct3D 9 and Direct3D 11 via DXVK and WineD3D.
    /// </summary>
    public const bool EnableD3DLegacySupport = false;

    /// <summary>
    /// OpenCL 1.2+ and Mesa Rusticl compute backends.
    /// </summary>
    public const bool EnableOpenClSupport = false;

    /// <summary>
    /// NVIDIA CUDA Driver API backend.
    /// </summary>
    public const bool EnableCudaSupport = false;

    /// <summary>
    /// AMD ROCm / HIP compute runtime backend.
    /// </summary>
    public const bool EnableRocmSupport = false;

    /// <summary>
    /// Intel oneAPI Level Zero compute backend.
    /// </summary>
    public const bool EnableOneApiSupport = false;

    /// <summary>
    /// Determines whether the requested backend is permitted to execute.
    /// </summary>
    public static bool IsBackendEnabled(TargetBackend backend, bool allowExperimental = false)
    {
        if (EnableExperimentalBackends || allowExperimental)
            return true;

        return backend switch
        {
            TargetBackend.Vk => EnableVulkanSupport,

            TargetBackend.Vkd3d or TargetBackend.Vkd3dP => EnableDx12Support,

            TargetBackend.Gl or TargetBackend.Gles or
            TargetBackend.Zink or TargetBackend.ZinkEs => EnableOpenGlSupport,

            TargetBackend.Dxvk9 or TargetBackend.Wd3d9 or
            TargetBackend.Dxvk11 or TargetBackend.Wd3d11 => EnableD3DLegacySupport,

            TargetBackend.Cl or TargetBackend.MesaCl => EnableOpenClSupport,
            TargetBackend.Cuda => EnableCudaSupport,
            TargetBackend.Rocm => EnableRocmSupport,
            TargetBackend.Oapi => EnableOneApiSupport,

            _ => false
        };
    }

    /// <summary>
    /// Returns the human-readable status for CLI diagnostics.
    /// </summary>
    public static string GetBackendStatus(TargetBackend backend)
    {
        if (IsBackendEnabled(backend))
            return "Active / Production Ready";

        if (backend is TargetBackend.Vkd3d or TargetBackend.Vkd3dP)
            return "In Development (Experimental)";

        return "Disabled by AppConfig Killswitch";
    }
}
