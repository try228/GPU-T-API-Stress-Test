using GpuT.Agent.Runtimes;

namespace GpuT.Agent.Core;

public enum TargetBackend
{
    // Native APIs
    Vk, Gl, Gles, Cl, MesaCl, Rocm, Oapi, Cuda,
    // Translators / Layers
    Dxvk, Vkd3d, Vkd3dP, Wd3d, Zink, ZinkEs
}

public static class BackendRouter
{
    public static TargetBackend ParseBackend(string? rawInput)
    {
        return (rawInput?.ToLowerInvariant().Trim()) switch
        {
            "vk" => TargetBackend.Vk,
            "gl" => TargetBackend.Gl,
            "gles" => TargetBackend.Gles,
            "cl" => TargetBackend.Cl,
            "mesa_cl" or "rusticl" => TargetBackend.MesaCl,
            "rocm" or "hip" => TargetBackend.Rocm,
            "oapi" or "oneapi" or "level0" => TargetBackend.Oapi,
            "cuda" => TargetBackend.Cuda,
            "dxvk" => TargetBackend.Dxvk,
            "vkd3d" => TargetBackend.Vkd3d,
            "vkd3d_p" or "vkd3d-proton" => TargetBackend.Vkd3dP,
            "wd3d" or "wined3d" => TargetBackend.Wd3d,
            "zink" => TargetBackend.Zink,
            "zink_es" => TargetBackend.ZinkEs,
            _ => TargetBackend.Vk
        };
    }

    public static void ApplyEnvironmentOverrides(TargetBackend backend)
    {
        switch (backend)
        {
            case TargetBackend.Zink:
            case TargetBackend.ZinkEs:
                Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
                Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "zink");
                Console.WriteLine("[EnvManager] Injected Mesa Override: MESA_LOADER_DRIVER_OVERRIDE=zink");
                break;

            case TargetBackend.MesaCl:
                Environment.SetEnvironmentVariable("RUSTICL_ENABLE", "all");
                Console.WriteLine("[EnvManager] Injected Rusticl Flag: RUSTICL_ENABLE=all");
                break;

            // ФИКС 100% IDLE ROCM: Отключаем поллинг очередей, включаем аппаратные прерывания
case TargetBackend.Rocm:
                Environment.SetEnvironmentVariable("GPU_MAX_HW_QUEUES", "1");
                Environment.SetEnvironmentVariable("HSA_ENABLE_INTERRUPT", "1");
                Console.WriteLine("[EnvManager] Injected ROCm Flags: GPU_MAX_HW_QUEUES=1, HSA_ENABLE_INTERRUPT=1");
                break;
        }
    }

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

        if (filtered.Count == 1)
        {
            Console.WriteLine($"[AutoSelector] Selected only available runtime: {filtered[0].Name}");
            return filtered[0];
        }

        Console.WriteLine("\n==================================================");
        Console.WriteLine($" Multiple compatible runtimes found for backend [{backend}]:");
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
            Console.Write("Invalid index. Try again: ");
        }
    }
}