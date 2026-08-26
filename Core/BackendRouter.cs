using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Core;

/// <summary>
/// Handles backend parsing, CLI routing, and driver environment overrides.
/// </summary>
public static class BackendRouter
{
    /// <summary>
    /// Parses a backend string representation into a strongly-typed enum.
    /// Defaults to Desktop OpenGL (TargetBackend.Gl) if no backend is specified.
    /// </summary>
    /// <param name="rawInput">The raw backend name from CLI or environment.</param>
    /// <returns>Resolved TargetBackend enum.</returns>
    public static TargetBackend ParseBackend(string? rawInput)
    {
        // Default backend is Desktop OpenGL as requested by maintainer
        if (string.IsNullOrWhiteSpace(rawInput)) return TargetBackend.Gl;

        string clean = rawInput.Trim().Trim('"', '\'', ' ').ToLowerInvariant();

        // Normalize accidental Cyrillic keyboard input
        clean = clean.Replace('с', 'c')
                     .Replace('о', 'o')
                     .Replace('р', 'p')
                     .Replace('а', 'a')
                     .Replace('е', 'e')
                     .Replace('х', 'x');

        return clean switch
        {
            "gl" or "opengl" => TargetBackend.Gl,
            "gles" or "opengles" or "gles3" or "opengl-es" => TargetBackend.Gles,
            "vk" or "vulkan" => TargetBackend.Vk,
            "zink" => TargetBackend.Zink,
            "zink_es" or "zink-es" or "zinkgles" or "zink_gles" => TargetBackend.ZinkEs,
            "cl" or "opencl" or "ocl" => TargetBackend.Cl,
            "mesa_cl" or "mesa-cl" or "mesacl" or "rusticl" or "rust_cl" or "rust-cl" => TargetBackend.MesaCl,
            "cuda" or "nv" or "nvidia" => TargetBackend.Cuda,
            "rocm" or "hip" or "amd" => TargetBackend.Rocm,
            "oapi" or "oneapi" or "level0" or "levelzero" or "ze" or "intel" => TargetBackend.Oapi,

            // Direct3D 9
            "dxvk_9" or "dxvk-9" or "dxvk9" or "dx9" or "d3d9" => TargetBackend.Dxvk9,
            "wd3d_9" or "wd3d-9" or "wined3d_9" or "wined3d-9" or "wined3d9" => TargetBackend.Wd3d9,

            // Direct3D 11
            "dxvk_11" or "dxvk-11" or "dxvk11" or "dxvk" or "dx11" or "d3d11" => TargetBackend.Dxvk11,
            "wd3d_11" or "wd3d-11" or "wined3d_11" or "wined3d-11" or "wined3d11" or "wd3d" or "wined3d" => TargetBackend.Wd3d11,

            // Direct3D 12
            "vkd3d" or "vkd3d_w" or "vkd3d-wine" => TargetBackend.Vkd3d,
            "vkd3d_p" or "vkd3d-proton" or "vkd3dp" or "dx12" or "d3d12" => TargetBackend.Vkd3dP,

            _ => throw new ArgumentException($"Unknown backend identifier '{rawInput}'. Supported: gl, gles, vk, zink, zink_es, cl, mesa_cl, cuda, rocm, oapi, dxvk_9, dxvk_11, wd3d_9, wd3d_11, vkd3d, vkd3d_p.")
        };
    }

    /// <summary>
    /// Applies driver-level environment variables required by specific layers (e.g. Zink, Rusticl).
    /// </summary>
    /// <param name="backend">Selected backend.</param>
    public static void ApplyEnvironmentOverrides(TargetBackend backend)
    {
        switch (backend)
        {
            case TargetBackend.Zink:
            case TargetBackend.ZinkEs:
                Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
                Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "zink");
                break;

            case TargetBackend.MesaCl:
                Environment.SetEnvironmentVariable("RUSTICL_ENABLE", "all");
                break;
        }
    }

    /// <summary>
    /// Resolves and filters compatible Wine or Proton environments based on backend requirements.
    /// </summary>
    public static RuntimeEnvironment? ResolveRuntime(TargetBackend backend, List<RuntimeEnvironment> available)
    {
        List<RuntimeEnvironment> filtered = backend switch
        {
            TargetBackend.Vkd3d => available.Where(r => r.Type == RuntimeType.Wine).ToList(),
            TargetBackend.Vkd3dP => available.Where(r => r.Type == RuntimeType.Proton).ToList(),
            TargetBackend.Dxvk9 or TargetBackend.Dxvk11 or TargetBackend.Wd3d9 or TargetBackend.Wd3d11 => available.ToList(),
            _ => []
        };

        if (filtered.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] No compatible Wine/Proton runtimes found for backend '{backend}'.");
            Console.ResetColor();
            return null;
        }

        if (filtered.Count == 1) return filtered[0];

        Console.WriteLine("\n==================================================");
        Console.WriteLine($" Compatible runtimes for [{backend}]:");
        for (int i = 0; i < filtered.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] [{filtered[i].Type}] {filtered[i].Name} ({filtered[i].ExecutablePath})");
        }
        Console.WriteLine("==================================================");
        Console.Write("Select Runtime (Enter number): ");

        while (true)
        {
            string? line = Console.ReadLine();
            if (int.TryParse(line, out int idx) && idx >= 1 && idx <= filtered.Count)
            {
                return filtered[idx - 1];
            }
            Console.Write("Invalid selection. Try again: ");
        }
    }
}