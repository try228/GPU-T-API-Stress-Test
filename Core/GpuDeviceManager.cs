using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Core;

/// <summary>
/// Represents a physical GPU detected on the host system with hardware PCI identifiers.
/// </summary>
public sealed record GpuDeviceDescriptor(
    int Index,
    string Name,
    string Vendor,
    uint VendorId,
    uint DeviceId,
    bool IsDiscrete,
    ulong VramSizeBytes
);

/// <summary>
/// Enumerates host GPU hardware and resolves user selection from CLI arguments.
/// </summary>
public static class GpuDeviceManager
{
    /// <summary>
    /// Enumerates all physical GPUs available via Vulkan.
    /// </summary>
    /// <returns>A list of detected GPU descriptors.</returns>
    public static unsafe List<GpuDeviceDescriptor> EnumerateGpus()
    {
        var list = new List<GpuDeviceDescriptor>();
        var vk = Vk.GetApi();

        InstanceCreateInfo instInfo = new() { SType = StructureType.InstanceCreateInfo };
        Instance instance;
        if (vk.CreateInstance(&instInfo, null, &instance) != Result.Success)
        {
            return list;
        }

        try
        {
            uint count = 0;
            vk.EnumeratePhysicalDevices(instance, &count, null);
            if (count > 0)
            {
                PhysicalDevice* pDevs = stackalloc PhysicalDevice[(int)count];
                vk.EnumeratePhysicalDevices(instance, &count, pDevs);

                for (uint i = 0; i < count; i++)
                {
                    PhysicalDeviceProperties props;
                    vk.GetPhysicalDeviceProperties(pDevs[i], &props);
                    string name = Marshal.PtrToStringAnsi((nint)props.DeviceName) ?? $"GPU #{i}";
                    bool isDiscrete = props.DeviceType == PhysicalDeviceType.DiscreteGpu;

                    PhysicalDeviceMemoryProperties memProps;
                    vk.GetPhysicalDeviceMemoryProperties(pDevs[i], &memProps);
                    ulong maxHeapSize = 0;
                    for (uint h = 0; h < memProps.MemoryHeapCount; h++)
                    {
                        if (memProps.MemoryHeaps[(int)h].Flags.HasFlag(MemoryHeapFlags.DeviceLocalBit))
                        {
                            maxHeapSize = Math.Max(maxHeapSize, memProps.MemoryHeaps[(int)h].Size);
                        }
                    }

                    string vendor = props.VendorID switch
                    {
                        0x1002 => "AMD",
                        0x10DE => "NVIDIA",
                        0x8086 => "Intel",
                        _ => "Unknown"
                    };

                    list.Add(new GpuDeviceDescriptor((int)i, name, vendor, props.VendorID, props.DeviceID, isDiscrete, maxHeapSize));
                }
            }
        }
        finally
        {
            vk.DestroyInstance(instance, null);
        }

        return list;
    }

    /// <summary>
    /// Prints all detected GPUs to standard output with their PCI IDs.
    /// </summary>
    public static void PrintGpuList()
    {
        var gpus = EnumerateGpus();
        Console.WriteLine("\n=== GPU-T Detected GPU Hardware ===");
        if (gpus.Count == 0)
        {
            Console.WriteLine("  No Vulkan-capable GPUs detected.");
            return;
        }

        for (int i = 0; i < gpus.Count; i++)
        {
            var g = gpus[i];
            double vramGb = g.VramSizeBytes / (1024.0 * 1024.0 * 1024.0);
            string type = g.IsDiscrete ? "Dedicated (dGPU)" : "Integrated (iGPU)";
            Console.WriteLine($"  [{g.Index}] {g.Name} ({g.Vendor} [{g.VendorId:x4}:{g.DeviceId:x4}]) - {type}, VRAM: {vramGb:F1} GB");
        }
        Console.WriteLine("===================================\n");
    }

    /// <summary>
    /// Resolves target GPU index from CLI argument without DRI_PRIME heuristics.
    /// </summary>
    /// <param name="gpuArg">Explicit CLI argument value (-g, --gpu).</param>
    /// <param name="available">List of detected GPUs.</param>
    /// <returns>Selected GPU index.</returns>
    public static int ResolveGpuIndex(string? gpuArg, List<GpuDeviceDescriptor> available)
    {
        if (available.Count == 0) return 0;

        // 1. Numeric index
        if (!string.IsNullOrWhiteSpace(gpuArg))
        {
            if (int.TryParse(gpuArg, out int parsedIdx))
            {
                int clamped = Math.Clamp(parsedIdx, 0, available.Count - 1);
                Console.WriteLine($"[GpuManager] Selected GPU by index: [{clamped}] {available[clamped].Name}");
                return clamped;
            }

            // 2. Name or Vendor substring match
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].Name.Contains(gpuArg, StringComparison.OrdinalIgnoreCase) ||
                    available[i].Vendor.Contains(gpuArg, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[GpuManager] Selected GPU by name match '{gpuArg}': [{i}] {available[i].Name}");
                    return i;
                }
            }
        }

        // 3. Default fallback: Discrete GPU with highest VRAM
        int bestIdx = 0;
        ulong bestVram = 0;
        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].IsDiscrete && available[i].VramSizeBytes > bestVram)
            {
                bestVram = available[i].VramSizeBytes;
                bestIdx = i;
            }
        }

        return bestIdx;
    }
}