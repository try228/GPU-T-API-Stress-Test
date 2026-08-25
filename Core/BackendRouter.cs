using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Core;

/// <summary>
/// Handles backend parsing, CLI routing, and driver environment overrides.
/// </summary>
public static class BackendRouter
{
    /// <summary>
    /// Parses a backend string representation into a strongly-typed enum.
    /// Handles Cyrillic lookalike characters (e.g. Russian 'с' instead of Latin 'c').
    /// </summary>
    /// <param name="rawInput">The raw backend name from CLI or environment.</param>
    /// <returns>Resolved TargetBackend enum.</returns>
    public static TargetBackend ParseBackend(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            throw new ArgumentException("No backend specified! Use -b <backend> or set API_backend env var.");

        string clean = rawInput.Trim().Trim('"', '\'', ' ').ToLowerInvariant();

        // Normalize common accidental Cyrillic keyboard input
        clean = clean.Replace('с', 'c')
                     .Replace('о', 'o')
                     .Replace('р', 'p')
                     .Replace('а', 'a')
                     .Replace('е', 'e')
                     .Replace('х', 'x');

        return clean switch
        {
            "vk" or "vulkan" => TargetBackend.Vk,
            "gl" or "opengl" => TargetBackend.Gl,
            "gles" or "opengles" or "gles3" or "opengl-es" => TargetBackend.Gles,
            "zink" => TargetBackend.Zink,
            "zink_es" or "zink-es" or "zinkgles" or "zink_gles" => TargetBackend.ZinkEs,
            "cl" or "opencl" or "ocl" => TargetBackend.Cl,
            "mesa_cl" or "mesa-cl" or "mesacl" or "rusticl" or "rust_cl" or "rust-cl" => TargetBackend.MesaCl,
            "cuda" or "nv" or "nvidia" => TargetBackend.Cuda,
            "rocm" or "hip" or "amd" => TargetBackend.Rocm,
            "oapi" or "oneapi" or "level0" or "levelzero" or "ze" or "intel" => TargetBackend.Oapi,
            "dxvk" => TargetBackend.Dxvk,
            "vkd3d" => TargetBackend.Vkd3d,
            "vkd3d_p" or "vkd3d-proton" => TargetBackend.Vkd3dP,
            "wd3d" or "wined3d" => TargetBackend.Wd3d,
            _ => throw new ArgumentException($"Unknown backend identifier '{rawInput}'. Supported: vk, gl, gles, zink, zink_es, cl, mesa_cl, cuda, rocm, oapi, dxvk, vkd3d, vkd3d_p, wd3d.")
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
            TargetBackend.Dxvk or TargetBackend.Wd3d => available.ToList(),
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