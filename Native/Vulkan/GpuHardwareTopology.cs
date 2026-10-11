using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Hardware topology and memory inspector dynamically querying physical GPU characteristics on Linux.
/// Queries standard Vulkan heaps, Linux DRM sysfs, and vendor-specific core properties.
/// </summary>
public sealed unsafe class GpuHardwareTopology
{
    /// <summary>Total dedicated physical VRAM in bytes.</summary>
    public ulong TotalVramBytes { get; private set; }

    /// <summary>Calculated buffer size to guarantee complete L3/Infinity Cache exhaustion.</summary>
    public ulong VramSweepSizeBytes { get; private set; }

    /// <summary>Total active compute units / workgroup processors on the silicon.</summary>
    public uint ComputeUnitCount { get; private set; }

    /// <summary>Dynamic workgroup dispatch count for ALU and L1/L2 stress tests.</summary>
    public uint WorkgroupsAluL1 { get; private set; }

    /// <summary>Dynamic workgroup dispatch count for L3 cache stress tests.</summary>
    public uint WorkgroupsL3 { get; private set; }

    /// <summary>Dynamic workgroup dispatch count for full VRAM bus saturation.</summary>
    public uint WorkgroupsVramBus { get; private set; }

    /// <summary>
    /// Probes the physical device and builds hardware-accurate runtime dimensions.
    /// </summary>
    public static GpuHardwareTopology Probe(Vk vk, PhysicalDevice physicalDevice, Instance instance)
    {
        var topology = new GpuHardwareTopology();
        topology.Initialize(vk, physicalDevice, instance);
        return topology;
    }

    private void Initialize(Vk vk, PhysicalDevice physicalDevice, Instance instance)
    {
        // 1. Query physical VRAM size across device-local heaps
        PhysicalDeviceMemoryProperties memProps;
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProps);

        ulong maxDeviceLocalHeap = 0;
        for (uint i = 0; i < memProps.MemoryHeapCount; i++)
        {
            if ((memProps.MemoryHeaps[(int)i].Flags & MemoryHeapFlags.DeviceLocalBit) != 0)
            {
                if (memProps.MemoryHeaps[(int)i].Size > maxDeviceLocalHeap)
                {
                    maxDeviceLocalHeap = memProps.MemoryHeaps[(int)i].Size;
                }
            }
        }

        TotalVramBytes = maxDeviceLocalHeap > 0 ? maxDeviceLocalHeap : 4UL * 1024 * 1024 * 1024;

        // On Linux, fallback to amdgpu sysfs if available to verify exact dedicated VRAM
        ulong sysfsVram = ReadLinuxAmdgpuVram();
        if (sysfsVram > 0)
        {
            TotalVramBytes = sysfsVram;
        }

        // 2. Compute dynamic VRAM sweep size (must vastly exceed L3 Infinity Cache / L2)
        // 16 GB GPUs (e.g. 7900 GRE) -> 1024 MB sweep; 8 GB GPUs -> 512 MB; <=4 GB -> 256 MB
        ulong targetSweep = TotalVramBytes / 16UL;
        VramSweepSizeBytes = Math.Clamp(targetSweep, 256UL * 1024 * 1024, 1024UL * 1024 * 1024);

        // Ensure 16-byte vector alignment and power-of-two granularity
        VramSweepSizeBytes = (VramSweepSizeBytes + 0x0FFFFFFUL) & ~0x0FFFFFFUL;

        // 3. Query Compute Units / SMs on Linux
        ComputeUnitCount = ProbeComputeUnitsLinux(vk, physicalDevice);

        // 4. Derive optimal dispatch workgroup dimensions (64 threads per workgroup)
        WorkgroupsAluL1 = ComputeUnitCount * 64u;
        WorkgroupsL3 = ComputeUnitCount * 32u;

        // In the VRAM sweep, each thread streams 8 consecutive uint4 vectors (128 bytes).
        // Half of the buffer is read, half is written (Read + Write = Total VramSweepSizeBytes).
        uint halfSweepBytes = (uint)(VramSweepSizeBytes / 2UL);
        uint threadsNeeded = halfSweepBytes / 128u;
        WorkgroupsVramBus = (threadsNeeded + 63u) / 64u;
    }

    private static uint ProbeComputeUnitsLinux(Vk vk, PhysicalDevice physicalDevice)
    {
        try
        {
            string[] drmDirs = Directory.GetDirectories("/sys/class/drm", "card*");
            foreach (var dir in drmDirs)
            {
                string cuPath = Path.Combine(dir, "device", "cu_info");
                if (File.Exists(cuPath))
                {
                    string text = File.ReadAllText(cuPath).Trim();
                    if (uint.TryParse(text, out uint cu) && cu > 0)
                        return cu;
                }
            }
        }
        catch { }

        PhysicalDeviceProperties props;
        vk.GetPhysicalDeviceProperties(physicalDevice, &props);

        if (props.VendorID == 0x1002) // AMD
        {
            return 80u; // Saturates RDNA 3 Navi 31/32
        }
        if (props.VendorID == 0x10DE) // NVIDIA
        {
            return 72u; // Saturates AD103/AD104
        }

        return 48u;
    }

    private static ulong ReadLinuxAmdgpuVram()
    {
        try
        {
            string[] drmDirs = Directory.GetDirectories("/sys/class/drm", "card*");
            foreach (var dir in drmDirs)
            {
                string vramPath = Path.Combine(dir, "device", "mem_info_vram_total");
                if (File.Exists(vramPath))
                {
                    string text = File.ReadAllText(vramPath).Trim();
                    if (ulong.TryParse(text, out ulong bytes) && bytes > 0)
                        return bytes;
                }
            }
        }
        catch { }

        return 0;
    }
}