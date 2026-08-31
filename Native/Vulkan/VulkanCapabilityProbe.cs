using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Mock GPU profiles for testing capability detection and synthetic architecture limits.
/// </summary>
public enum MockGpuTarget
{
    None,
    NvidiaBlackwell,
    IntelBattlemage,
    AmdRdna3,
    LegacyPascal
}

/// <summary>
/// Hardware capability prober that discovers hardware cache hierarchy, ISA instruction support,
/// and matrix/tensor engines via Vulkan API queries and declarative architecture tables.
/// </summary>
public sealed unsafe class VulkanCapabilityProbe
{
    /// <summary>
    /// Declarative hardware cache size rule defined in kilobytes.
    /// </summary>
    private readonly record struct CacheRule(string Pattern, uint L2Kb, uint L3Kb);

    private static readonly string[] SoftwareRenderers = ["LLVMPIPE", "LAVAPIPE", "SWIFTSHADER", "VK-GL-CTS"];

    // APUs with dedicated Infinity Cache / MALL
    private static readonly string[] AmdHaloApuAllowlist = ["8040S", "8050S", "8060S", "8065S", "STRIX HALO", "AI MAX"];

    // AMD Cache Rules: Instinct / RDNA 4 through GCN 1.0 architectures (RADV, AMDVLK, AMDGPU-PRO)
    private static readonly CacheRule[] AmdRules =
    [
        // Instinct / CDNA Compute Accelerators
        new(@"\bMI300[AX]?\b|\bGFX942\b|\bGFX940\b",             L2Kb: 4096, L3Kb: 256 * 1024),
        new(@"\bMI2[05]0X?\b|\bALDEBARAN\b|\bGFX90A\b",          L2Kb: 8192, L3Kb: 0),
        new(@"\bMI100\b|\bARCTURUS\b|\bGFX908\b",                L2Kb: 8192, L3Kb: 0),
        new(@"\bMI[56]0\b|\bMI25\b",                             L2Kb: 4096, L3Kb: 0),

        // RDNA 3.5 Halo APU (Dedicated Infinity Cache / MALL)
        new(@"\b806[05]S\b|STRIX\s*HALO|AI\s*MAX\+?\s*395",     L2Kb: 4096, L3Kb: 64 * 1024),
        new(@"\b80[45]0S\b",                                     L2Kb: 4096, L3Kb: 32 * 1024),

        // RDNA 4 (Navi 4x)
        new(@"\b9070\s*XT\b|\b9070\b|\bNAVI\s*48\b|\bGFX1200\b", L2Kb: 8192, L3Kb: 64 * 1024),
        new(@"\b9060\b|\b9050\b|\bNAVI\s*44\b|\bGFX1201\b",      L2Kb: 4096, L3Kb: 32 * 1024),

        // RDNA 3 (RX 7000, Radeon PRO W7000, Mobile / Navi 3x)
        new(@"\b7900\s*XTX\b|\bW7900\b|\bNAVI\s*31\b|\bGFX1100\b", L2Kb: 6144, L3Kb: 96 * 1024),
        new(@"\b7900\s*XT\b|\b7900M\b",                             L2Kb: 6144, L3Kb: 80 * 1024),
        new(@"\b7900\s*GRE\b|\b7800\s*XT\b|\bW7800\b|\bNAVI\s*32\b|\bGFX1101\b", L2Kb: 6144, L3Kb: 64 * 1024),
        new(@"\b7700\s*XT\b|\bW7700\b",                             L2Kb: 4096, L3Kb: 48 * 1024),
        new(@"\b7600\s*XT\b|\b7600\b|\b7600M\b|\b7700S\b|\b7600S\b|\bW7600\b|\bW7500\b|\bNAVI\s*33\b|\bGFX1102\b", L2Kb: 2048, L3Kb: 32 * 1024),

        // RDNA 2 (RX 6000, Radeon PRO W6000, Mobile / Navi 2x)
        new(@"\b6950\s*XT\b|\b6900\s*XT\b|\b6800\s*XT\b|\b6800\b|\bW6800\b|\bW6900X\b|\bNAVI\s*21\b|\bGFX1030\b", L2Kb: 4096, L3Kb: 128 * 1024),
        new(@"\b6750\s*XT\b|\b6700\s*XT\b|\b6850M\b|\b6800M\b|\bW6700\b|\bNAVI\s*22\b|\bGFX1031\b",     L2Kb: 3072, L3Kb: 96 * 1024),
        new(@"\b6700\b|\b6700M\b",                                                                       L2Kb: 3072, L3Kb: 80 * 1024),
        new(@"\b6650\s*XT\b|\b6600\s*XT\b|\b6600\b|\b6650M\b|\b6600M\b|\bW6600\b|\bNAVI\s*23\b|\bGFX1032\b", L2Kb: 2048, L3Kb: 32 * 1024),
        new(@"\b6500\s*XT\b|\b6400\b|\b6500M\b|\b6300M\b|\bW6400\b|\bNAVI\s*24\b|\bGFX1034\b",         L2Kb: 1024, L3Kb: 16 * 1024),
        new(@"\b6300\b",                                                                                  L2Kb: 1024, L3Kb: 8 * 1024),

        // RDNA 1 (RX 5000, Radeon PRO W5000, Mobile / Navi 1x)
        new(@"\b5700\s*XT\b|\b5700\b|\b5700M\b|\b5600\s*XT\b|\b5600M\b|\bW5700\b|\bNAVI\s*10\b|\bGFX1010\b", L2Kb: 4096, L3Kb: 0),
        new(@"\b5500\s*XT\b|\b5500\b|\b5500M\b|\b5300M\b|\bW5500\b|\bNAVI\s*14\b|\bGFX1012\b",               L2Kb: 2048, L3Kb: 0),

        // GCN 5.0 (Vega, Radeon VII, Radeon PRO Vega, Instinct MI60/50/25)
        new(@"\bRADEON\s*VII\b|\bVEGA\s*20\b|\bGFX906\b|\bPRO\s*VEGA\s*II\b", L2Kb: 4096, L3Kb: 0),
        new(@"\bVEGA\s*64\b|\bVEGA\s*56\b|\bVEGA\s*10\b|\bGFX900\b|\bWX\s*9100\b|\bWX\s*8200\b|\bPRO\s*SSG\b", L2Kb: 4096, L3Kb: 0),
        new(@"\bVEGA\b|\bRAVEN\b|\bRENOIR\b|\bCEZANNE\b|\bBARCELO\b",         L2Kb: 512,  L3Kb: 0),

        // GCN 4.0 (Polaris, Radeon PRO WX)
        new(@"\b590\b|\b580\b|\b570\b|\b480\b|\b470\b|\bWX\s*7100\b|\bWX\s*5100\b|\bPOLARIS\s*10\b|\bPOLARIS\s*20\b|\bPOLARIS\s*30\b|\bGFX803\b", L2Kb: 2048, L3Kb: 0),
        new(@"\b560\b|\b460\b|\bWX\s*4100\b|\bPOLARIS\s*11\b|\bPOLARIS\s*21\b",                                                                     L2Kb: 1024, L3Kb: 0),
        new(@"\b550\b|\b540\b|\bWX\s*3200\b|\bWX\s*3100\b|\bWX\s*2100\b|\bPOLARIS\s*12\b",                                                         L2Kb: 512,  L3Kb: 0),

        // GCN 3.0 (Fiji, Tonga, Antigua, Carrizo, FirePro W7100)
        new(@"\bFURY\b|\bNANO\b|\bFIJI\b",                                       L2Kb: 2048, L3Kb: 0),
        new(@"\b380\b|\b285\b|\bTONGA\b|\bANTIGUA\b|\bCARRIZO\b|\bW7100\b",       L2Kb: 512,  L3Kb: 0),

        // GCN 2.0 (Hawaii, Bonaire, Kaveri, FirePro W9100/W8100/S9150)
        new(@"\b390\s*X?\b|\b290\s*X?\b|\bHAWAII\b|\bGRENDEL\b|\bW9100\b|\bW8100\b|\bS91[57]0\b", L2Kb: 1024, L3Kb: 0),
        new(@"\b360\b|\b260\s*X?\b|\b7790\b|\bBONAIRE\b|\bKAVERI\b|\bW5100\b",                    L2Kb: 512,  L3Kb: 0),

        // GCN 1.0 (Tahiti, Pitcairn, Cape Verde, Oland, FirePro W7000/W5000)
        new(@"\b7970\b|\b7950\b|\b280\s*X?\b|\bTAHITI\b|\bW7000\b",              L2Kb: 384,  L3Kb: 0),
        new(@"\b7870\b|\b7850\b|\b270\s*X?\b|\bPITCAIRN\b|\bCURACAO\b|\bW5000\b", L2Kb: 512,  L3Kb: 0),
        new(@"\b7770\b|\b7750\b|\b250\s*X?\b|\bCAPE\s*VERDE\b|\bW4100\b",         L2Kb: 512,  L3Kb: 0),
        new(@"\b7730\b|\b240\b|\bOLAND\b|\bHAINAN\b",                           L2Kb: 256,  L3Kb: 0)
    ];

    // NVIDIA Cache Rules: Datacenter, Workstation, and GeForce architectures
    private static readonly CacheRule[] NvidiaRules =
    [
        // Datacenter & Compute Accelerators
        new(@"\bB200\b|\bB100\b|\bGB200\b",                                       L2Kb: 128 * 1024, L3Kb: 0),
        new(@"\bH200\b|\bH100\b|\bGH100\b",                                       L2Kb: 50 * 1024,  L3Kb: 0),
        new(@"\bA100\b|\bA800\b|\bGA100\b",                                       L2Kb: 40 * 1024,  L3Kb: 0),
        new(@"\bTITAN\s*V\b|\bV100\b|\bGV100\b",                                  L2Kb: 6144,       L3Kb: 0),
        new(@"\bP100\b|\bGP100\b",                                                L2Kb: 4096,       L3Kb: 0),
        new(@"\bK80\b|\bK40\b|\bK20\b|\bGK210\b|\bGK110B\b",                      L2Kb: 1536,       L3Kb: 0),

        // Blackwell (RTX 5000 Desktop, Mobile, Workstation)
        new(@"\b5090\b|\bGB202\b",                                                L2Kb: 128 * 1024, L3Kb: 0),
        new(@"\b5080\b|\bGB203\b",                                                L2Kb: 64 * 1024,  L3Kb: 0),
        new(@"\b5070\s*TI\b|\b5070\b|\bGB205\b",                                  L2Kb: 48 * 1024,  L3Kb: 0),
        new(@"\b5060\b|\bGB206\b|\bGB207\b",                                      L2Kb: 32 * 1024,  L3Kb: 0),

        // Ada Lovelace (RTX 4000, RTX 6000-2000 Ada Generation, Mobile)
        new(@"\b6000\s*ADA\b|\b4090\b|\bAD102\b",                                 L2Kb: 72 * 1024,  L3Kb: 0),
        new(@"\b5000\s*ADA\b|\b4080\s*SUPER\b|\b4080\b|\bAD103\b",                L2Kb: 64 * 1024,  L3Kb: 0),
        new(@"\b4500\s*ADA\b|\b4000\s*ADA\b|\b4070\s*TI\s*SUPER\b|\b4070\s*TI\b|\b4070\s*SUPER\b|\bAD104\b", L2Kb: 48 * 1024, L3Kb: 0),
        new(@"\b4070\b",                                                          L2Kb: 36 * 1024,  L3Kb: 0),
        new(@"\b3500\s*ADA\b|\b4060\s*TI\b|\bAD106\b",                            L2Kb: 32 * 1024,  L3Kb: 0),
        new(@"\b2000\s*ADA\b|\b4060\b|\b4050\b|\bAD107\b",                       L2Kb: 24 * 1024,  L3Kb: 0),

        // Ampere (RTX 3000, RTX A6000-A2000, A40/A30/A16/A10/A2, Mobile)
        new(@"\b3090\s*TI\b|\b3090\b|\b3080\s*TI\b|\bA6000\b|\bA5500\b|\bA5000\b|\bA40\b|\bA10\b|\bGA102\b", L2Kb: 6144, L3Kb: 0),
        new(@"\b3080\b|\bA30\b",                                                  L2Kb: 5120,       L3Kb: 0),
        new(@"\b3070\s*TI\b|\b3070\b|\b3060\s*TI\b|\bA4500\b|\bA4000\b|\bA3000\b|\bGA104\b",                L2Kb: 4096, L3Kb: 0),
        new(@"\b3060\b|\bA2000\b|\bGA106\b",                                      L2Kb: 3072,       L3Kb: 0),
        new(@"\b3050\b|\bA1000\b|\bA500\b|\bA2\b|\bGA107\b",                     L2Kb: 2048,       L3Kb: 0),

        // Turing (RTX 2000, Quadro RTX 8000-3000, GTX 1600, T1000-T400, Mobile)
        new(@"\b2080\s*TI\b|\bTITAN\s*RTX\b|\bRTX\s*8000\b|\bRTX\s*6000\b|\bTU102\b", L2Kb: 5632, L3Kb: 0),
        new(@"\b2080\b|\b2070\s*SUPER\b|\bRTX\s*5000\b|\bRTX\s*4000\b|\bTU104\b",      L2Kb: 4096, L3Kb: 0),
        new(@"\b2070\b|\b2060\s*SUPER\b|\b2060\b|\bRTX\s*3000\b|\bTU106\b",              L2Kb: 3072, L3Kb: 0),
        new(@"\b1660\s*TI\b|\b1660\s*SUPER\b|\b1660\b|\bTU116\b",                        L2Kb: 1536, L3Kb: 0),
        new(@"\b1650\b|\bT1200\b|\bT1000\b|\bT600\b|\bT400\b|\bTU117\b",                L2Kb: 1024, L3Kb: 0),

        // Pascal (GTX 1000, Quadro P6000-P400, Tesla P40/P4, Mobile)
        new(@"\b1080\s*TI\b|\bTITAN\s*X\b|\bTITAN\s*XP\b|\bP6000\b|\bP40\b|\bGP102\b", L2Kb: 3072, L3Kb: 0),
        new(@"\b1080\b|\b1070\s*TI\b|\b1070\b|\bP5200\b|\bP5000\b|\bP4200\b|\bP4000\b|\bP3200\b|\bP3000\b|\bP4\b|\bGP104\b", L2Kb: 2048, L3Kb: 0),
        new(@"\b1060\b|\bP2000\b|\bGP106\b",                                     L2Kb: 1536,       L3Kb: 0),
        new(@"\b1050\s*TI\b|\b1050\b|\bP1000\b|\bP620\b|\bP600\b|\bP500\b|\bP400\b|\bGP107\b", L2Kb: 1024, L3Kb: 0),
        new(@"\b1030\b|\bGP108\b",                                                L2Kb: 512,        L3Kb: 0),

        // Maxwell (GTX 900, Quadro M6000-M1000, GTX 750 Ti, Mobile)
        new(@"\bTITAN\s*X\b|\b980\s*TI\b|\bM6000\b|\bGM200\b",                   L2Kb: 3072,       L3Kb: 0),
        new(@"\b980\b|\b970\b|\b980M\b|\bM5500\b|\bM5000\b|\bM4000\b|\bGM204\b", L2Kb: 2048,       L3Kb: 0),
        new(@"\b960\b|\b950\b|\b970M\b|\bM3000\b|\bM2000\b|\bGM206\b",           L2Kb: 1024,       L3Kb: 0),
        new(@"\b750\s*TI\b|\b750\b|\b960M\b|\b950M\b|\bGM107\b",                 L2Kb: 2048,       L3Kb: 0),

        // Kepler (GTX 700, 600, Quadro K6000-K600, Tesla K40/K80, Mobile)
        new(@"\bTITAN\s*BLACK\b|\bTITAN\s*Z\b|\bTITAN\b|\b780\s*TI\b|\b780\b|\bK6000\b|\bK5200\b|\bGK110\b", L2Kb: 1536, L3Kb: 0),
        new(@"\b770\b|\b760\s*TI\b|\b680\b|\b670\b|\b660\s*TI\b|\b880M\b|\b870M\b|\b780M\b|\bK5000\b|\bK4200\b|\bK4000\b|\bGK104\b", L2Kb: 512, L3Kb: 0),
        new(@"\b760\b|\b660\b|\b650\s*TI\s*BOOST\b|\b860M\b|\b770M\b|\bK3100\b|\bK2200\b|\bK2000\b|\bGK106\b", L2Kb: 384, L3Kb: 0),
        new(@"\b650\s*TI\b|\b650\b|\b740\b|\b730\b|\bK1100\b|\bK620\b|\bK600\b|\bK420\b|\bGK107\b|\bGK208\b", L2Kb: 256, L3Kb: 0)
    ];

    // Intel Cache Rules: Data Center GPU Max, Flex, Arc, Iris Xe
    private static readonly CacheRule[] IntelRules =
    [
        // Data Center GPU Max (Xe-HPC / Ponte Vecchio / PVC)
        new(@"\bMAX\s*1550\b|\bMAX\s*1350\b|\bMAX\s*1100\b|\bPONTE\s*VECCHIO\b|\bPVC\b", L2Kb: 408 * 1024, L3Kb: 0),

        // Data Center GPU Flex (Xe-HPG / Arctic Sound-M / ATS-M)
        new(@"\bFLEX\s*170\b|\bARCTIC\s*SOUND\b|\bATS-M\b",                               L2Kb: 16 * 1024,  L3Kb: 0),
        new(@"\bFLEX\s*140\b",                                                            L2Kb: 8 * 1024,   L3Kb: 0),

        // Battlemage & Lunar Lake (Xe2-HPG / BMG)
        new(@"\bB580\b|\bB570\b|\bBATTLEMAGE\b|\bBMG\b",                                  L2Kb: 16 * 1024,  L3Kb: 0),
        new(@"\bLUNAR\s*LAKE\b|\bLNL\b",                                                   L2Kb: 8 * 1024,   L3Kb: 0),

        // Arc & Arc Pro (Xe-HPG / DG2)
        new(@"\bA770\b|\bA750\b|\bPRO\s*A60\b|\bALCHEMIST\b|\bACM-G10\b|\bDG2-512\b",     L2Kb: 16 * 1024,  L3Kb: 0),
        new(@"\bA580\b|\bPRO\s*A60M\b",                                                   L2Kb: 8 * 1024,   L3Kb: 0),
        new(@"\bA380\b|\bA310\b|\bPRO\s*A50\b|\bPRO\s*A40\b|\bPRO\s*A30M\b|\bACM-G11\b|\bDG2-128\b", L2Kb: 4096, L3Kb: 0),

        // Gen12 Xe-LP (TigerLake, AlderLake, RaptorLake / UHD 770 / Iris Xe)
        new(@"\bIRIS\s*XE\b|\bUHD\s*7[37]0\b|\bTGL\b|\bADL\b|\bRPL\b",                    L2Kb: 3840,       L3Kb: 0),

        // Gen11 (IceLake / Iris Plus)
        new(@"\bICL\b|\bICE\s*LAKE\b|\bIRIS\s*PLUS\b",                                   L2Kb: 3072,       L3Kb: 0),

        // Gen9 / Gen9.5 (Skylake, KabyLake, CoffeeLake, CometLake)
        new(@"\bSKL\b|\bKBL\b|\bCFL\b|\bCML\b|\bHD\s*5[123]0\b|\bHD\s*6[23]0\b|\bUHD\s*6[23]0\b", L2Kb: 768, L3Kb: 0),

        // Gen8 (Broadwell / BDW)
        new(@"\bBDW\b|\bBROADWELL\b|\bHD\s*5[35]00\b|\bHD\s*6000\b|\bIRIS\s*6100\b|\bIRIS\s*PRO\s*6200\b", L2Kb: 768, L3Kb: 0),

        // Gen7.5 (Haswell / HSW)
        new(@"\bHSW\b|\bHASWELL\b|\bHD\s*4[46]00\b|\bHD\s*5000\b|\bIRIS\s*5100\b|\bIRIS\s*PRO\s*5200\b",   L2Kb: 512, L3Kb: 0)
    ];

    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the PCI vendor identifier reported by the Vulkan driver.
    /// </summary>
    public uint VendorId { get; private set; }

    /// <summary>
    /// Gets the physical device type reported by the Vulkan driver.
    /// </summary>
    public PhysicalDeviceType DeviceType { get; private set; }

    /// <summary>
    /// Gets the formatted display name of the GPU device.
    /// </summary>
    public string DeviceName { get; private set; } = "Vulkan GPU";

    /// <summary>
    /// Gets the probed L2 cache capacity in bytes.
    /// </summary>
    public uint L2CacheSizeBytes { get; private set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Gets the probed L3 / Infinity Cache capacity in bytes, or 0 if unsupported.
    /// </summary>
    public uint L3CacheSizeBytes { get; private set; } = 0;

    /// <summary>
    /// Gets a value indicating whether the GPU possesses dedicated L3 / Infinity Cache silicon.
    /// </summary>
    public bool HasL3InfinityCache => L3CacheSizeBytes > 0;

    public bool HasFloat16 { get; private set; }
    public bool HasFloat64 { get; private set; }
    public bool HasInt8 { get; private set; }
    public bool HasInt16 { get; private set; }
    public bool HasInt64 { get; private set; }
    public bool HasIntegerDotProduct { get; private set; }
    public bool HasBFloat16 { get; private set; }
    public bool HasFloat8 { get; private set; }

    public bool HasCoopMatrixKHR { get; private set; }
    public bool HasCoopMatrixNV { get; private set; }
    public bool HasCoopMatrixNV2 { get; private set; }
    public bool HasIntelInt4 { get; private set; }

    public bool HasMatFP16 { get; private set; }
    public bool HasMatBF16 { get; private set; }
    public bool HasMatFP32 { get; private set; }
    public bool HasMatFP64 { get; private set; }
    public bool HasMatFP8_E4M3 { get; private set; }
    public bool HasMatFP8_E5M2 { get; private set; }
    public bool HasMatINT8 { get; private set; }
    public bool HasMatINT4 => HasIntelInt4;
    public bool HasMatINT16 { get; private set; }
    public bool HasMatINT32 { get; private set; }
    public bool HasMatINT64 { get; private set; }

    public bool HasRayQuery { get; private set; }
    public bool HasRTPipeline { get; private set; }
    public bool HasSER { get; private set; }
    public bool HasOMM { get; private set; }
    public bool HasRTPositionFetch { get; private set; }
    public bool HasNVRayTracing { get; private set; }

    public bool HasVideoDecodeH264 { get; private set; }
    public bool HasVideoDecodeH265 { get; private set; }
    public bool HasVideoDecodeAV1 { get; private set; }
    public bool HasVideoDecodeVP9 { get; private set; }
    public bool HasVideoEncodeH264 { get; private set; }
    public bool HasVideoEncodeH265 { get; private set; }
    public bool HasVideoEncodeAV1 { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanCapabilityProbe"/> class.
    /// </summary>
    public VulkanCapabilityProbe(Vk vk, PhysicalDevice physicalDevice, MockGpuTarget mockTarget = MockGpuTarget.None)
    {
        PhysicalDeviceProperties props;
        vk.GetPhysicalDeviceProperties(physicalDevice, &props);
        VendorId = props.VendorID;
        DeviceType = props.DeviceType;
        DeviceName = Marshal.PtrToStringAnsi((nint)props.DeviceName) ?? "Vulkan GPU";

        if (mockTarget != MockGpuTarget.None)
        {
            ApplyMockProfile(mockTarget);
            return;
        }

        ProbeArchitecture();
        ProbeExtensions(vk, physicalDevice);
        ProbeFeatures(vk, physicalDevice);
        ProbeMatrixCapabilities(vk, physicalDevice);
    }

    private void ProbeArchitecture()
    {
        string name = DeviceName.ToUpperInvariant();

        // 1. Software renderers
        if (SoftwareRenderers.Any(name.Contains) || VendorId == 0x10005 || DeviceType == PhysicalDeviceType.Cpu)
        {
            L2CacheSizeBytes = 512 * 1024;
            L3CacheSizeBytes = 0;
            return;
        }

        // 2. Default cache values based on device type
        uint defaultL2Kb = DeviceType == PhysicalDeviceType.IntegratedGpu ? 512u : 2048u;

        var (rules, vendorDefaultL2Kb) = VendorId switch
        {
            0x1002 => (AmdRules, defaultL2Kb),
            0x10DE => (NvidiaRules, 1536u),
            0x8086 => (IntelRules, defaultL2Kb),
            _      => (Array.Empty<CacheRule>(), defaultL2Kb)
        };

        // 3. Integrated graphics exclusion flag
        bool isIntegratedApu = DeviceType == PhysicalDeviceType.IntegratedGpu;
        bool isHaloChip = AmdHaloApuAllowlist.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));

        // 4. Pattern matching through architectural rule tables
        foreach (var rule in rules)
        {
            if (Regex.IsMatch(name, rule.Pattern, RegexOptions.IgnoreCase))
            {
                L2CacheSizeBytes = rule.L2Kb * 1024;
                L3CacheSizeBytes = (isIntegratedApu && !isHaloChip) ? 0 : rule.L3Kb * 1024;
                return;
            }
        }

        // 5. Fallback
        L2CacheSizeBytes = vendorDefaultL2Kb * 1024;
        L3CacheSizeBytes = 0;
    }

    private void ProbeExtensions(Vk vk, PhysicalDevice physicalDevice)
    {
        uint count = 0;
        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, null);
        if (count == 0) return;

        ExtensionProperties* pExts = stackalloc ExtensionProperties[(int)count];
        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, pExts);

        for (uint i = 0; i < count; i++)
        {
            string name = Marshal.PtrToStringAnsi((nint)pExts[i].ExtensionName) ?? "";
            if (!string.IsNullOrEmpty(name)) _supportedExtensions.Add(name);
        }

        HasFloat16 = _supportedExtensions.Contains("VK_KHR_shader_float16_int8");
        HasInt8 = _supportedExtensions.Contains("VK_KHR_shader_float16_int8");
        HasIntegerDotProduct = _supportedExtensions.Contains("VK_KHR_shader_integer_dot_product");
        HasBFloat16 = _supportedExtensions.Contains("VK_KHR_shader_bfloat16");
        HasFloat8 = _supportedExtensions.Contains("VK_EXT_shader_float8");

        HasCoopMatrixKHR = _supportedExtensions.Contains("VK_KHR_cooperative_matrix");
        HasCoopMatrixNV = _supportedExtensions.Contains("VK_NV_cooperative_matrix");
        HasCoopMatrixNV2 = _supportedExtensions.Contains("VK_NV_cooperative_matrix2");
        
        HasIntelInt4 = (VendorId == 0x8086) && HasCoopMatrixKHR && _supportedExtensions.Contains("VK_INTEL_shader_integer_functions2");

        HasRayQuery = _supportedExtensions.Contains("VK_KHR_ray_query");
        HasRTPipeline = _supportedExtensions.Contains("VK_KHR_ray_tracing_pipeline");
        HasSER = _supportedExtensions.Contains("VK_KHR_ray_tracing_invocation_reorder") || _supportedExtensions.Contains("VK_NV_ray_tracing_invocation_reorder");
        HasOMM = _supportedExtensions.Contains("VK_KHR_opacity_micromap");
        HasRTPositionFetch = _supportedExtensions.Contains("VK_KHR_ray_tracing_position_fetch");
        HasNVRayTracing = _supportedExtensions.Contains("VK_NV_ray_tracing");

        HasVideoDecodeH264 = _supportedExtensions.Contains("VK_KHR_video_decode_h264");
        HasVideoDecodeH265 = _supportedExtensions.Contains("VK_KHR_video_decode_h265");
        HasVideoDecodeAV1 = _supportedExtensions.Contains("VK_KHR_video_decode_av1");
        HasVideoDecodeVP9 = _supportedExtensions.Contains("VK_KHR_video_decode_vp9");
        HasVideoEncodeH264 = _supportedExtensions.Contains("VK_KHR_video_encode_h264");
        HasVideoEncodeH265 = _supportedExtensions.Contains("VK_KHR_video_encode_h265");
        HasVideoEncodeAV1 = _supportedExtensions.Contains("VK_KHR_video_encode_av1");
    }

    private void ProbeFeatures(Vk vk, PhysicalDevice physicalDevice)
    {
        PhysicalDeviceFeatures features;
        vk.GetPhysicalDeviceFeatures(physicalDevice, &features);
        HasFloat64 = features.ShaderFloat64;
        HasInt64 = features.ShaderInt64;
        HasInt16 = features.ShaderInt16;
    }

    private void ProbeMatrixCapabilities(Vk vk, PhysicalDevice physicalDevice)
    {
        HasMatFP16 = HasMatBF16 = HasMatFP32 = HasMatFP64 = false;
        HasMatFP8_E4M3 = HasMatFP8_E5M2 = HasMatINT8 = false;
        HasMatINT16 = HasMatINT32 = HasMatINT64 = false;

        if (!HasCoopMatrixKHR && !HasCoopMatrixNV)
        {
            return;
        }

        bool queriedSuccessfully = false;

        // 1. Standard query via VK_KHR_cooperative_matrix
        if (HasCoopMatrixKHR)
        {
            nint pfnKhr = vk.Context.GetProcAddress("vkGetPhysicalDeviceCooperativeMatrixPropertiesKHR");
            if (pfnKhr != 0)
            {
                var getPropertiesKhr = (delegate* unmanaged[Cdecl]<PhysicalDevice, uint*, CooperativeMatrixPropertiesKHR*, Result>)pfnKhr;
                uint propCount = 0;
                if (getPropertiesKhr(physicalDevice, &propCount, null) == Result.Success && propCount > 0)
                {
                    var properties = stackalloc CooperativeMatrixPropertiesKHR[(int)propCount];
                    for (int i = 0; i < propCount; i++)
                    {
                        properties[i].SType = StructureType.CooperativeMatrixPropertiesKhr;
                    }

                    if (getPropertiesKhr(physicalDevice, &propCount, properties) == Result.Success)
                    {
                        for (int i = 0; i < propCount; i++)
                        {
                            // GEMM precision is determined by input operands AType (A * B), not accumulator CType
                            RegisterComponentTypeKHR(properties[i].AType);
                        }
                        queriedSuccessfully = true;
                    }
                }
            }
        }

        // 2. Query via NVIDIA extension VK_NV_cooperative_matrix
        if (HasCoopMatrixNV && !queriedSuccessfully)
        {
            nint pfnNv = vk.Context.GetProcAddress("vkGetPhysicalDeviceCooperativeMatrixPropertiesNV");
            if (pfnNv != 0)
            {
                var getPropertiesNv = (delegate* unmanaged[Cdecl]<PhysicalDevice, uint*, CooperativeMatrixPropertiesNV*, Result>)pfnNv;
                uint propCount = 0;
                if (getPropertiesNv(physicalDevice, &propCount, null) == Result.Success && propCount > 0)
                {
                    var properties = stackalloc CooperativeMatrixPropertiesNV[(int)propCount];
                    for (int i = 0; i < propCount; i++)
                    {
                        properties[i].SType = (StructureType)1000249001;
                    }

                    if (getPropertiesNv(physicalDevice, &propCount, properties) == Result.Success)
                    {
                        for (int i = 0; i < propCount; i++)
                        {
                            RegisterComponentTypeNV(properties[i].AType);
                        }
                        queriedSuccessfully = true;
                    }
                }
            }
        }

        // 3. Fallback only if driver did not expose matrix properties via API
        if (!queriedSuccessfully)
        {
            HasMatFP16 = true;
            HasMatBF16 = HasBFloat16;
            HasMatINT8 = true;
            HasMatFP8_E4M3 = HasFloat8;
            HasMatFP8_E5M2 = HasFloat8;
        }
    }

    private void RegisterComponentTypeKHR(ComponentTypeKHR type)
    {
        switch (type)
        {
            case ComponentTypeKHR.Float16Khr: HasMatFP16 = true; break;
            case ComponentTypeKHR.Float32Khr: HasMatFP32 = true; break;
            case ComponentTypeKHR.Float64Khr: HasMatFP64 = true; break;
            case ComponentTypeKHR.Sint8Khr:
            case ComponentTypeKHR.Uint8Khr:   HasMatINT8 = true; break;
            case ComponentTypeKHR.Sint16Khr:
            case ComponentTypeKHR.Uint16Khr:  HasMatINT16 = true; break;
            case ComponentTypeKHR.Sint32Khr:
            case ComponentTypeKHR.Uint32Khr:  HasMatINT32 = true; break;
            case ComponentTypeKHR.Sint64Khr:
            case ComponentTypeKHR.Uint64Khr:  HasMatINT64 = true; break;
            default:
                int val = (int)type;
                if (val == 1000141000) HasMatBF16 = true;         // VK_COMPONENT_TYPE_BFLOAT16_KHR
                else if (val == 1000491002) HasMatFP8_E4M3 = true; // VK_COMPONENT_TYPE_FLOAT8_E4M3_EXT
                else if (val == 1000491003) HasMatFP8_E5M2 = true; // VK_COMPONENT_TYPE_FLOAT8_E5M2_EXT
                break;
        }
    }

    private void RegisterComponentTypeNV(ComponentTypeNV type)
    {
        RegisterComponentTypeKHR((ComponentTypeKHR)(int)type);
    }

    private void ApplyMockProfile(MockGpuTarget target)
    {
        HasFloat64 = true; HasInt64 = true; HasInt16 = true;

        if (target == MockGpuTarget.NvidiaBlackwell)
        {
            VendorId = 0x10DE; DeviceName = "NVIDIA GeForce RTX 5090 (Mock)";
            DeviceType = PhysicalDeviceType.DiscreteGpu;
            L2CacheSizeBytes = 128 * 1024 * 1024; L3CacheSizeBytes = 0;
            HasFloat16 = true; HasInt8 = true; HasIntegerDotProduct = true; HasBFloat16 = true; HasFloat8 = true;
            HasCoopMatrixKHR = true; HasCoopMatrixNV = true; HasCoopMatrixNV2 = true;
            HasMatFP16 = true; HasMatBF16 = true; HasMatINT8 = true;
            HasMatFP8_E4M3 = true; HasMatFP8_E5M2 = true;
            HasRayQuery = true; HasRTPipeline = true; HasSER = true; HasOMM = true; HasRTPositionFetch = true;
            HasVideoDecodeAV1 = true; HasVideoEncodeAV1 = true;
        }
        else if (target == MockGpuTarget.IntelBattlemage)
        {
            VendorId = 0x8086; DeviceName = "Intel Arc B580 (Mock)";
            DeviceType = PhysicalDeviceType.DiscreteGpu;
            L2CacheSizeBytes = 16 * 1024 * 1024; L3CacheSizeBytes = 0;
            HasFloat16 = true; HasInt8 = true; HasIntegerDotProduct = true; HasBFloat16 = true;
            HasCoopMatrixKHR = true; HasIntelInt4 = true;
            HasMatFP16 = true; HasMatBF16 = true; HasMatINT8 = true;
            HasRayQuery = true; HasRTPipeline = true; HasVideoDecodeVP9 = true; HasVideoEncodeAV1 = true;
        }
        else if (target == MockGpuTarget.AmdRdna3)
        {
            VendorId = 0x1002; DeviceName = "AMD Radeon RX 7900 GRE (Mock)";
            DeviceType = PhysicalDeviceType.DiscreteGpu;
            L2CacheSizeBytes = 6 * 1024 * 1024; L3CacheSizeBytes = 64 * 1024 * 1024;
            HasFloat16 = true; HasInt8 = true; HasIntegerDotProduct = true; HasBFloat16 = true;
            HasCoopMatrixKHR = true;
            HasMatFP16 = true; HasMatBF16 = true; HasMatINT8 = true;
            HasRayQuery = true; HasRTPipeline = true;
            HasVideoDecodeAV1 = true; HasVideoEncodeAV1 = true;
        }
        else if (target == MockGpuTarget.LegacyPascal)
        {
            VendorId = 0x10DE; DeviceName = "NVIDIA GeForce GTX 1060 (Mock)";
            DeviceType = PhysicalDeviceType.DiscreteGpu;
            L2CacheSizeBytes = 1536 * 1024; L3CacheSizeBytes = 0;
            HasFloat16 = true; HasInt8 = true; HasIntegerDotProduct = true;
        }
    }
}