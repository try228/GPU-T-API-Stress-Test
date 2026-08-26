namespace GPU_T.StressTest.Core;

/// <summary>
/// Supported graphics, compute, and Direct3D translation backends.
/// </summary>
public enum TargetBackend
{
    // Native Graphics APIs
    Vk,
    Gl,
    Gles,

    // Translation / Compatibility Layers (Mesa Gallium)
    Zink,
    ZinkEs,

    // Compute APIs
    Cl,
    MesaCl,
    Cuda,
    Rocm,
    Oapi,

    // Direct3D 9
    Dxvk9,
    Wd3d9,

    // Direct3D 11
    Dxvk11,
    Wd3d11,

    // Direct3D 12
    Vkd3d,
    Vkd3dP
}