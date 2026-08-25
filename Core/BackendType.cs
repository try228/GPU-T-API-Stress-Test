namespace GPU_T.StressTest.Core;

/// <summary>
/// Supported graphics and compute backends for stress testing.
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

    // Windows D3D Compatibility Layers (Wine / Proton)
    Dxvk,
    Vkd3d,
    Vkd3dP,
    Wd3d
}