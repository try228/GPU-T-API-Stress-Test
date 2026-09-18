namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Color and styling themes matching individual graphics and compute APIs.
/// </summary>
public enum ThemePalette
{
    Vulkan,
    OpenGL,
    OpenGLES,
    Cuda,
    Rocm,
    OneApi,
    OpenCL,
    Rusticl,
    Dxvk,
    Vkd3d,
    WineD3d
}

/// <summary>
/// Software pixel rendering engine for procedural HUD, controls, and modal matrix configuration.
/// 100% unified across Vulkan, OpenGL, CUDA, ROCm, oneAPI, and OpenCL backends.
/// </summary>
public static class PixelUiEngine
{
    public const int BaseWidth = 900;
    public const int BaseHeight = 550;

    public struct Rect
    {
        public int X, Y, W, H;
        public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public bool Contains(int px, int py) => px >= X && px < X + W && py >= Y && py < Y + H;
    }

    // Main Control Bar Rectangles
    public static readonly Rect BtnStartStop = new(35, 75, 195, 38);
    public static readonly Rect Btn10s = new(240, 75, 60, 38);
    public static readonly Rect Btn30s = new(308, 75, 60, 38);
    public static readonly Rect Btn60s = new(376, 75, 60, 38);
    public static readonly Rect BtnUnlimited = new(444, 75, 105, 38);
    public static readonly Rect InputCustom = new(560, 75, 160, 38);
    public static readonly Rect BtnMinus = new(730, 75, 45, 38);
    public static readonly Rect BtnPlus = new(783, 75, 45, 38);
    public static readonly Rect BtnSettings = new(834, 75, 32, 38);

    // Modal Preset Buttons
    public static readonly Rect ModalPreBase = new(45, 54, 90, 22);
    public static readonly Rect ModalPreGaming = new(140, 54, 80, 22);
    public static readonly Rect ModalPreGamingRT = new(225, 54, 105, 22);
    public static readonly Rect ModalPreAI = new(335, 54, 90, 22);
    public static readonly Rect ModalPreMem = new(430, 54, 80, 22);
    public static readonly Rect ModalPreVid = new(515, 54, 70, 22);
    public static readonly Rect ModalPreFull = new(590, 54, 100, 22);
    public static readonly Rect ModalClose = new(735, 54, 120, 22);

    private static readonly byte[][] GlyphTable = new byte[128][];
    private static readonly byte[] EmptyGlyph = new byte[8];

    static PixelUiEngine()
    {
        for (int i = 0; i < 128; i++) GlyphTable[i] = EmptyGlyph;

        GlyphTable['A'] = [0x18, 0x3C, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00];
        GlyphTable['B'] = [0x7C, 0x66, 0x7C, 0x66, 0x66, 0x66, 0x7C, 0x00];
        GlyphTable['C'] = [0x3C, 0x66, 0x60, 0x60, 0x60, 0x66, 0x3C, 0x00];
        GlyphTable['D'] = [0x78, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0x78, 0x00];
        GlyphTable['E'] = [0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x7E, 0x00];
        GlyphTable['F'] = [0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x60, 0x00];
        GlyphTable['G'] = [0x3C, 0x66, 0x60, 0x6E, 0x66, 0x66, 0x3E, 0x00];
        GlyphTable['H'] = [0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x66, 0x00];
        GlyphTable['I'] = [0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00];
        GlyphTable['J'] = [0x1E, 0x0C, 0x0C, 0x0C, 0x0C, 0x6C, 0x38, 0x00];
        GlyphTable['K'] = [0x66, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0x66, 0x00];
        GlyphTable['L'] = [0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00];
        GlyphTable['M'] = [0x63, 0x77, 0x7F, 0x6B, 0x63, 0x63, 0x63, 0x00];
        GlyphTable['N'] = [0x66, 0x76, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x00];
        GlyphTable['O'] = [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00];
        GlyphTable['P'] = [0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0x00];
        GlyphTable['Q'] = [0x3C, 0x66, 0x66, 0x66, 0x6A, 0x6C, 0x36, 0x00];
        GlyphTable['R'] = [0x7C, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0x66, 0x00];
        GlyphTable['S'] = [0x3C, 0x66, 0x30, 0x1C, 0x06, 0x66, 0x3C, 0x00];
        GlyphTable['T'] = [0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00];
        GlyphTable['U'] = [0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00];
        GlyphTable['V'] = [0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x00];
        GlyphTable['W'] = [0x63, 0x63, 0x63, 0x6B, 0x7F, 0x77, 0x63, 0x00];
        GlyphTable['X'] = [0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0x00];
        GlyphTable['Y'] = [0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 0x00];
        GlyphTable['Z'] = [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 0x00];
        GlyphTable['0'] = [0x3C, 0x66, 0x6E, 0x76, 0x66, 0x66, 0x3C, 0x00];
        GlyphTable['1'] = [0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00];
        GlyphTable['2'] = [0x3C, 0x66, 0x06, 0x1C, 0x30, 0x60, 0x7E, 0x00];
        GlyphTable['3'] = [0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00];
        GlyphTable['4'] = [0x0C, 0x1C, 0x34, 0x64, 0x7E, 0x04, 0x04, 0x00];
        GlyphTable['5'] = [0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00];
        GlyphTable['6'] = [0x3C, 0x66, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0x00];
        GlyphTable['7'] = [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00];
        GlyphTable['8'] = [0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00];
        GlyphTable['9'] = [0x3C, 0x66, 0x66, 0x3E, 0x06, 0x66, 0x3C, 0x00];
        GlyphTable['+'] = [0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00];
        GlyphTable['-'] = [0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00];
        GlyphTable[':'] = [0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00];
        GlyphTable['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00];
        GlyphTable['/'] = [0x02, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x40, 0x00];
        GlyphTable['|'] = [0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00];
        GlyphTable['_'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00];
        GlyphTable['['] = [0x1E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x1E, 0x00];
        GlyphTable[']'] = [0x78, 0x18, 0x18, 0x18, 0x18, 0x18, 0x78, 0x00];
        GlyphTable['%'] = [0x62, 0x64, 0x08, 0x10, 0x20, 0x26, 0x46, 0x00];
        GlyphTable['~'] = [0x00, 0x32, 0x4C, 0x00, 0x00, 0x00, 0x00, 0x00];
        GlyphTable['>'] = [0x60, 0x30, 0x18, 0x0C, 0x18, 0x30, 0x60, 0x00];
        GlyphTable['<'] = [0x06, 0x0C, 0x18, 0x30, 0x18, 0x0C, 0x06, 0x00];
    }

    public static string FormatGpuName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Generic GPU";
        raw = raw.Trim();
        raw = raw.Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase);
        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        if (raw.Length > 40) raw = raw.Substring(0, 37) + "...";
        return raw.Trim();
    }

    public static unsafe void Render(
        uint* buffer, int width, int height,
        string apiTitle, string gpuName, bool isBenchmarking, int durationSec,
        string customText, bool isCustomFocused,
        double elapsedSec, double dps, double totalTops,
        string? hwSensorStr,
        ThemePalette theme,
        int mouseX, int mouseY, float animTime,
        bool isSettingsOpen = false,
        VulkanTestRegistry? registry = null,
        double fp32Tflops = 0.0,
        double fp64Tflops = 0.0,
        double fp16Tflops = 0.0,
        double bf16Tflops = 0.0,
        double int32Tiops = 0.0,
        double int64Tiops = 0.0,
        double int16Tiops = 0.0,
        double int8Tiops = 0.0,
        double dp2aTops = 0.0,
        double dp4aTops = 0.0,
        double tmuGtexels = 0.0,
        double memBwGbs = 0.0,
        double cacheL1L2Tbs = 0.0,
        double cacheL3Tbs = 0.0,
        double latencyNs = 0.0,
        double matFp16Tops = 0.0,
        double matBf16Tops = 0.0,
        double matInt8Tops = 0.0)
    {
        RenderGpuZBackground(buffer, width, height, isBenchmarking ? animTime : 0.0f, isBenchmarking, theme);

        uint accentColor = GetThemeAccent(theme);
        uint activeBtnBg = GetThemeActiveButtonBg(theme);

        // Header Panel
        FillRectAlpha(buffer, width, 0, 0, width, 55, 0xDD0D1117);
        DrawLine(buffer, width, 0, 55, width, 55, 0xFF30363D);
        DrawString(buffer, width, 25, 12, $"GPU-T RENDER TEST  |  {apiTitle.ToUpperInvariant()}", 0xFFE6EDF3, 2);
        
        string cleanName = FormatGpuName(gpuName);
        DrawString(buffer, width, 25, 34, $"DEVICE: {cleanName}   •   STATUS: {(isBenchmarking ? "100% STRESS RUNNING" : "IDLE / STANDBY")}", isBenchmarking ? 0xFF3FB950 : accentColor, 1);

        // Controls
        bool hoverStart = BtnStartStop.Contains(mouseX, mouseY);
        uint startBg = isBenchmarking ? (hoverStart ? 0xFFDA3633 : 0xFFA40E26) : (hoverStart ? 0xFF2EA043 : 0xFF238636);
        FillRectAlpha(buffer, width, BtnStartStop.X, BtnStartStop.Y, BtnStartStop.W, BtnStartStop.H, startBg);
        DrawRect(buffer, width, BtnStartStop.X, BtnStartStop.Y, BtnStartStop.W, BtnStartStop.H, isBenchmarking ? 0xFFF85149 : 0xFF3FB950);
        DrawString(buffer, width, BtnStartStop.X + 35, BtnStartStop.Y + 12, isBenchmarking ? "STOP" : "START", 0xFFFFFFFF, 1);

        bool isPreset = durationSec is 10 or 30 or 60 or 0 && !isCustomFocused;
        DrawPresetButton(buffer, width, Btn10s, "10S", durationSec == 10 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, Btn30s, "30S", durationSec == 30 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, Btn60s, "60S", durationSec == 60 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, BtnUnlimited, "UNLIMITED", durationSec == 0 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);

        bool isCustomActive = isCustomFocused || (!isPreset && durationSec > 0);
        uint inputBg = isCustomFocused ? 0xEE161B22 : (isCustomActive ? activeBtnBg : 0xAA21262D);
        FillRectAlpha(buffer, width, InputCustom.X, InputCustom.Y, InputCustom.W, InputCustom.H, inputBg);
        DrawRect(buffer, width, InputCustom.X, InputCustom.Y, InputCustom.W, InputCustom.H, isCustomActive ? accentColor : 0xFF30363D);
        bool cursorVisible = isCustomFocused && ((int)(animTime * 4) % 2 == 0);
        string inputDisplay = $"SEC: {(string.IsNullOrEmpty(customText) ? durationSec.ToString() : customText)}{(cursorVisible ? "_" : " ")}";
        DrawString(buffer, width, InputCustom.X + 15, InputCustom.Y + 12, inputDisplay, isCustomActive ? 0xFFFFFFFF : 0xFFC9D1D9, 1);

        DrawPresetButton(buffer, width, BtnMinus, "-", false, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, BtnPlus, "+", false, mouseX, mouseY, accentColor, activeBtnBg);

        if (registry != null)
        {
            bool hoverSettings = BtnSettings.Contains(mouseX, mouseY);
            FillRectAlpha(buffer, width, BtnSettings.X, BtnSettings.Y, BtnSettings.W, BtnSettings.H, isSettingsOpen ? activeBtnBg : (hoverSettings ? 0xDD30363D : 0xAA21262D));
            DrawRect(buffer, width, BtnSettings.X, BtnSettings.Y, BtnSettings.W, BtnSettings.H, isSettingsOpen ? accentColor : 0xFF30363D);
            DrawString(buffer, width, BtnSettings.X + 5, BtnSettings.Y + 12, isBenchmarking ? "LOCK" : "CFG", isSettingsOpen ? 0xFFFFFFFF : 0xFFE6EDF3, 1);
        }

        // Telemetry HUD Panel
        int hudY = 355, hudH = 168;
        FillRectAlpha(buffer, width, 35, hudY, 830, hudH, 0xEE0D1117);
        DrawRect(buffer, width, 35, hudY, 830, hudH, isBenchmarking ? accentColor : 0xFF30363D);

        string durStr = durationSec == 0 ? $"{elapsedSec:F1}s / Unlimited" : $"{elapsedSec:F1}s / {durationSec}s";
        DrawString(buffer, width, 50, hudY + 10, $"BENCHMARK TIME: {durStr}   •   TOTAL DISPATCH RATE: {dps:F0} Dispatches/s", 0xFFF0F6FC, 1);

        bool hasGranularCompute = (fp32Tflops > 0 || fp64Tflops > 0 || fp16Tflops > 0 || bf16Tflops > 0 ||
                                   int32Tiops > 0 || int64Tiops > 0 || int16Tiops > 0 || int8Tiops > 0 ||
                                   dp2aTops > 0 || dp4aTops > 0 || tmuGtexels > 0 ||
                                   matFp16Tops > 0 || matBf16Tops > 0 || matInt8Tops > 0);
        bool hasGranularMemory = (cacheL1L2Tbs > 0 || cacheL3Tbs > 0 || memBwGbs > 0 || latencyNs > 0);

        if (hasGranularCompute || hasGranularMemory)
        {
            List<string> floatList = new();
            if (fp32Tflops > 0) floatList.Add($"FP32: {fp32Tflops:F2} TFLOPS");
            if (fp16Tflops > 0) floatList.Add($"FP16: {fp16Tflops:F2} TFLOPS");
            if (bf16Tflops > 0) floatList.Add($"BF16: {bf16Tflops:F2} TFLOPS");
            if (fp64Tflops > 0) floatList.Add($"FP64: {fp64Tflops:F2} TFLOPS");
            string lineFloats = floatList.Count > 0 ? string.Join("   •   ", floatList) : "STANDBY";
            DrawString(buffer, width, 50, hudY + 29, $"ALU FLOATS   |   {lineFloats}", isBenchmarking ? 0xFFE3B341 : 0xFF8B949E, 1);

            List<string> intList = new();
            if (int32Tiops > 0) intList.Add($"INT32: {int32Tiops:F2} TIOPS");
            if (int16Tiops > 0) intList.Add($"INT16: {int16Tiops:F2} TIOPS");
            if (int64Tiops > 0) intList.Add($"INT64: {int64Tiops:F2} TIOPS");
            if (int8Tiops > 0)  intList.Add($"INT8: {int8Tiops:F2} TIOPS");
            string lineInts = intList.Count > 0 ? string.Join("   •   ", intList) : "STANDBY";
            DrawString(buffer, width, 50, hudY + 47, $"ALU INTS     |   {lineInts}", isBenchmarking ? 0xFFFFB74D : 0xFF8B949E, 1);

            List<string> tensorList = new();
            if (matFp16Tops > 0) tensorList.Add($"MAT-FP16: {matFp16Tops:F2} TFLOPS");
            if (matBf16Tops > 0) tensorList.Add($"MAT-BF16: {matBf16Tops:F2} TFLOPS");
            if (matInt8Tops > 0) tensorList.Add($"MAT-INT8: {matInt8Tops:F2} TIOPS");
            if (dp4aTops > 0)    tensorList.Add($"DP4A: {dp4aTops:F2} TIOPS");
            if (dp2aTops > 0)    tensorList.Add($"DP2A: {dp2aTops:F2} TIOPS");
            if (tmuGtexels > 0)  tensorList.Add($"TMU: {tmuGtexels:F1} GTex/s");
            string lineTensor = tensorList.Count > 0 ? string.Join("   •   ", tensorList) : "STANDBY";
            DrawString(buffer, width, 50, hudY + 65, $"TENSOR & AI  |   {lineTensor}", isBenchmarking ? 0xFFFF8A65 : 0xFF8B949E, 1);

            List<string> memList = new();
            if (cacheL1L2Tbs > 0) memList.Add($"L1/L2: {cacheL1L2Tbs:F2} TB/s");
            if (cacheL3Tbs > 0)   memList.Add($"L3: {cacheL3Tbs:F2} TB/s");
            if (memBwGbs > 0)     memList.Add($"VRAM: {memBwGbs:F1} GB/s");
            if (latencyNs > 0)    memList.Add($"LATENCY: {latencyNs:F1} ns");
            string lineMem = memList.Count > 0 ? string.Join("   •   ", memList) : "100% STABLE";
            DrawString(buffer, width, 50, hudY + 83, $"MEMORY FAB.  |   {lineMem}", isBenchmarking ? 0xFF58A6FF : 0xFF8B949E, 1);
        }
        else
        {
            string loadMetric = isBenchmarking ? $"COMPUTE LOAD: ~{totalTops:F2} TOPS / TFLOPS" : "COMPUTE LOAD: IDLE (STANDBY)";
            DrawString(buffer, width, 50, hudY + 35, loadMetric, isBenchmarking ? 0xFFE3B341 : 0xFF8B949E, 1);

            if (!string.IsNullOrEmpty(hwSensorStr))
                DrawString(buffer, width, 50, hudY + 58, $"HARDWARE TELEMETRY: {hwSensorStr}", accentColor, 1);
            else
                DrawString(buffer, width, 50, hudY + 58, "COMPUTE PIPELINE: 100% Silicon Saturation (Active)", isBenchmarking ? accentColor : 0xFF8B949E, 1);
        }

        string activeStatus = registry != null 
            ? $"ACTIVE ENG.  |   {registry.GetActiveCount()} Silicon Workloads Engaged   •   Direct Native Vulkan Execution"
            : "PIPELINE STATUS: Direct Native Vulkan Hardware Execution";
        DrawString(buffer, width, 50, hudY + 104, activeStatus, accentColor, 1);

        if (durationSec > 0 && isBenchmarking)
        {
            float progress = Math.Clamp((float)(elapsedSec / durationSec), 0f, 1f);
            FillRectAlpha(buffer, width, 50, hudY + 126, 800, 6, 0xFF21262D);
            FillRectAlpha(buffer, width, 50, hudY + 126, (int)(800 * progress), 6, accentColor);
        }

        DrawString(buffer, width, 35, 530, registry != null 
            ? "Click [CFG] to configure test matrix. Press SPACE to Start/Stop. ESC to Exit." 
            : "Click buttons to control benchmark. Press SPACE to Start/Stop. ESC to Exit.", 0xFF6E7681, 1);

        if (isSettingsOpen && registry != null)
        {
            RenderSettingsModal(buffer, width, height, registry, mouseX, mouseY, accentColor, activeBtnBg, isBenchmarking);
        }
    }

    private static int GetCategoryColumn(string category) => category switch
    {
        "ALU (FLOAT)" or "ALU (INT)" or "VRAM & CACHES" => 0,
        "MATRIX TYPES" or "MATRIX MODS" or "VULKAN VIDEO" => 1,
        "RAY TRACING" or "ROP: COLOR & MRT" or "DEPTH / STENCIL" or "TMU TEXTURES" => 2,
        _ => 0
    };

    private static unsafe void RenderSettingsModal(uint* buffer, int width, int height, VulkanTestRegistry registry, int mouseX, int mouseY, uint accentCol, uint activeBg, bool isBenchmarking)
    {
        FillRectAlpha(buffer, width, 0, 0, width, height, 0xBB000000);
        int mx = 30, my = 15, mw = 840, mh = 515;
        FillRectAlpha(buffer, width, mx, my, mw, mh, 0xF50D1117);
        DrawRect(buffer, width, mx, my, mw, mh, accentCol);

        DrawString(buffer, width, mx + 15, my + 14, isBenchmarking ? "STRESS MATRIX [LOCKED WHILE RUNNING]" : "STRESS TEST WORKLOAD MATRIX", isBenchmarking ? 0xFFF85149 : 0xFFFFFFFF, 1);

        DrawPresetButton(buffer, width, ModalPreBase, "BASE FP32", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreGaming, "GAMING", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreGamingRT, "GAMING+RT", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreAI, "AI/MATRIX", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreMem, "MEMORY", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreVid, "VIDEO", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalPreFull, "FULL BURN", false, mouseX, mouseY, accentCol, activeBg);
        DrawPresetButton(buffer, width, ModalClose, "CLOSE [X]", true, mouseX, mouseY, accentCol, 0xFF238636);

        DrawLine(buffer, width, mx, my + 62, mx + mw, my + 62, 0xFF30363D);

        int startY = my + 70, colWidth = 265, colGap = 275;
        int[] colRows = new int[3];
        string[] lastCatInCol = new string[3] { "", "", "" };

        for (int i = 0; i < registry.Items.Count; i++)
        {
            var item = registry.Items[i];
            int col = GetCategoryColumn(item.Category);

            if (item.Category != lastCatInCol[col])
            {
                if (colRows[col] > 0) colRows[col]++;
                lastCatInCol[col] = item.Category;
                DrawString(buffer, width, mx + 15 + col * colGap, startY + colRows[col] * 15, item.Category, accentCol, 1);
                colRows[col]++;
            }

            int itemX = mx + 15 + col * colGap, itemY = startY + colRows[col] * 15;
            Rect itemRect = new(itemX, itemY, colWidth, 14);

            if (itemRect.Contains(mouseX, mouseY) && item.IsSupported && !isBenchmarking)
            {
                FillRectAlpha(buffer, width, itemRect.X, itemRect.Y, itemRect.W, itemRect.H, 0x4430363D);
            }

            string displayName = !item.IsSupported ? $"[-] {item.Name} (N/A)" : (item.IsChecked ? $"[V] {item.Name}" : $"[ ] {item.Name}");
            uint textColor = !item.IsSupported ? 0xFF484F58 : (item.IsChecked ? 0xFFFFFFFF : 0xFF8B949E);
            DrawString(buffer, width, itemX, itemY + 1, displayName, textColor, 1);
            colRows[col]++;
        }
    }

    public static void HandleSettingsClick(int mouseX, int mouseY, VulkanTestRegistry registry, ref bool isSettingsOpen, bool isBenchmarking = false)
    {
        if (ModalClose.Contains(mouseX, mouseY)) { isSettingsOpen = false; return; }
        if (isBenchmarking) return;

        if (ModalPreBase.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.DefaultFP32); return; }
        if (ModalPreGaming.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.Gaming); return; }
        if (ModalPreGamingRT.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.GamingRayTracing); return; }
        if (ModalPreAI.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.MatrixAI); return; }
        if (ModalPreMem.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.MemoryCache); return; }
        if (ModalPreVid.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.VideoEngine); return; }
        if (ModalPreFull.Contains(mouseX, mouseY)) { registry.ApplyPreset(StressPreset.FullSiliconBurn); return; }

        int mx = 30, my = 15, startY = my + 70, colWidth = 265, colGap = 275;
        int[] colRows = new int[3];
        string[] lastCatInCol = new string[3] { "", "", "" };

        for (int i = 0; i < registry.Items.Count; i++)
        {
            var item = registry.Items[i];
            int col = GetCategoryColumn(item.Category);

            if (item.Category != lastCatInCol[col])
            {
                if (colRows[col] > 0) colRows[col]++;
                lastCatInCol[col] = item.Category;
                colRows[col]++;
            }

            Rect itemRect = new(mx + 15 + col * colGap, startY + colRows[col] * 15, colWidth, 14);
            if (itemRect.Contains(mouseX, mouseY) && item.IsSupported)
            {
                item.IsChecked = !item.IsChecked;
                return;
            }
            colRows[col]++;
        }
    }

    private static unsafe void RenderGpuZBackground(uint* buffer, int width, int height, float time, bool isStress, ThemePalette theme)
    {
        float invW = 1.0f / width, invH = 1.0f / height;
        for (int y = 0; y < height; y++)
        {
            float ny = (y * invH) * 2.0f - 1.0f;
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                float nx = (x * invW) * 2.0f - 1.0f;
                float wave1 = MathF.Sin(nx * 4.0f + time * 1.8f);
                float wave2 = MathF.Cos(ny * 4.0f - time * 1.5f);
                float norm = (wave1 + wave2) * 0.25f + 0.5f;

                byte r = 0, g = 0, b = 0;
                if (isStress)
                {
                    switch (theme)
                    {
                        case ThemePalette.Vulkan:
                            r = (byte)Math.Clamp((int)(200 + 55 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(40 + 110 * (MathF.Sin(time * 1.5f + nx * 2.0f) * 0.5f + 0.5f) * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(10 + 30 * (1.0f - norm)), 0, 255);
                            break;
                        case ThemePalette.OpenCL:
                            r = (byte)Math.Clamp((int)(15 + 50 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(150 + 105 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(140 + 115 * (MathF.Sin(time * 1.4f + ny * 1.8f) * 0.5f + 0.5f)), 0, 255);
                            break;
                        case ThemePalette.Cuda:
                            r = (byte)Math.Clamp((int)(10 + 40 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(160 + 95 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(20 + 50 * norm), 0, 255);
                            break;
                        case ThemePalette.Rocm:
                            r = (byte)Math.Clamp((int)(210 + 45 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(15 + 35 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(25 + 45 * norm), 0, 255);
                            break;
                        default:
                            r = (byte)Math.Clamp((int)(15 + 45 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(60 + 130 * (MathF.Sin(time * 1.5f + nx * 2.0f) * 0.5f + 0.5f)), 0, 255);
                            b = (byte)Math.Clamp((int)(180 + 75 * norm), 0, 255);
                            break;
                    }
                }
                else
                {
                    float baseGrad = (ny * 0.5f + 0.5f);
                    switch (theme)
                    {
                        case ThemePalette.Vulkan:
                            r = (byte)(28 + 22 * baseGrad); g = (byte)(12 + 10 * baseGrad); b = (byte)(12 + 10 * baseGrad);
                            break;
                        case ThemePalette.OpenCL:
                            r = (byte)(10 + 8 * baseGrad); g = (byte)(24 + 20 * baseGrad); b = (byte)(26 + 22 * baseGrad);
                            break;
                        case ThemePalette.Cuda:
                            r = (byte)(12 + 10 * baseGrad); g = (byte)(26 + 24 * baseGrad); b = (byte)(14 + 10 * baseGrad);
                            break;
                        case ThemePalette.Rocm:
                            r = (byte)(30 + 22 * baseGrad); g = (byte)(12 + 10 * baseGrad); b = (byte)(16 + 12 * baseGrad);
                            break;
                        default:
                            r = (byte)(12 + 10 * baseGrad); g = (byte)(16 + 18 * baseGrad); b = (byte)(26 + 32 * baseGrad);
                            break;
                    }
                }

                buffer[rowOffset + x] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
            }
        }
    }

    private static uint GetThemeAccent(ThemePalette theme) => theme switch
    {
        ThemePalette.Vulkan => 0xFFFF5722,
        ThemePalette.OpenCL => 0xFF2DD4BF,
        ThemePalette.Cuda   => 0xFF00E676,
        ThemePalette.Rocm   => 0xFFFF1744,
        _                   => 0xFF2979FF
    };

    private static uint GetThemeActiveButtonBg(ThemePalette theme) => theme switch
    {
        ThemePalette.Vulkan => 0xEEB7300D,
        ThemePalette.OpenCL => 0xEE00695C,
        ThemePalette.Cuda   => 0xEE007E33,
        ThemePalette.Rocm   => 0xEEB71C1C,
        _                   => 0xEE0D47A1
    };

    private static unsafe void DrawPresetButton(uint* buffer, int width, Rect rect, string text, bool isSelected, int mx, int my, uint accentCol, uint activeBg)
    {
        bool hover = rect.Contains(mx, my);
        FillRectAlpha(buffer, width, rect.X, rect.Y, rect.W, rect.H, isSelected ? activeBg : (hover ? 0xDD30363D : 0xAA21262D));
        DrawRect(buffer, width, rect.X, rect.Y, rect.W, rect.H, isSelected ? accentCol : (hover ? 0xFF8B949E : 0xFF30363D));
        DrawString(buffer, width, rect.X + (rect.W - text.Length * 8) / 2, rect.Y + (rect.H - 8) / 2, text, 0xFFFFFFFF, 1);
    }

    private static unsafe void FillRectAlpha(uint* buffer, int bufW, int x, int y, int w, int h, uint col)
    {
        int startX = Math.Max(0, x);
        int startY = Math.Max(0, y);
        int endX = Math.Min(BaseWidth, x + w);
        int endY = Math.Min(BaseHeight, y + h);

        if (startX >= endX || startY >= endY) return;

        byte a = (byte)(col >> 24);
        if (a == 255)
        {
            for (int j = startY; j < endY; j++)
            {
                int row = j * bufW;
                for (int i = startX; i < endX; i++)
                    buffer[row + i] = col;
            }
            return;
        }

        float alpha = a / 255.0f, invA = 1.0f - alpha;
        uint srcR = (col >> 16) & 0xFF, srcG = (col >> 8) & 0xFF, srcB = col & 0xFF;

        for (int j = startY; j < endY; j++)
        {
            int row = j * bufW;
            for (int i = startX; i < endX; i++)
            {
                uint dst = buffer[row + i];
                uint outR = (uint)(srcR * alpha + ((dst >> 16) & 0xFF) * invA);
                uint outG = (uint)(srcG * alpha + ((dst >> 8) & 0xFF) * invA);
                uint outB = (uint)(srcB * alpha + (dst & 0xFF) * invA);
                buffer[row + i] = 0xFF000000 | (outR << 16) | (outG << 8) | outB;
            }
        }
    }

    private static unsafe void DrawRect(uint* buffer, int bufW, int x, int y, int w, int h, uint color)
    {
        DrawLine(buffer, bufW, x, y, x + w, y, color);
        DrawLine(buffer, bufW, x, y + h - 1, x + w, y + h - 1, color);
        DrawLine(buffer, bufW, x, y, x, y + h, color);
        DrawLine(buffer, bufW, x + w - 1, y, x + w - 1, y + h, color);
    }

    private static unsafe void DrawLine(uint* buffer, int bufW, int x0, int y0, int x1, int y1, uint color)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < BaseWidth && y0 >= 0 && y0 < BaseHeight) buffer[y0 * bufW + x0] = color;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static unsafe void DrawString(uint* buffer, int bufW, int x, int y, string text, uint color, int scale)
    {
        for (int c = 0; c < text.Length; c++)
        {
            char rawCh = text[c];
            char ch = char.ToUpperInvariant(rawCh);
            byte[] glyph = ((int)ch < 128) ? GlyphTable[(int)ch] : (rawCh == '•' ? [0x00, 0x18, 0x3C, 0x3C, 0x18, 0x00, 0x00, 0x00] : EmptyGlyph);

            for (int r = 0; r < 8; r++)
            {
                byte row = glyph[r];
                for (int b = 0; b < 8; b++)
                {
                    if ((row & (1 << (7 - b))) != 0)
                    {
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int px = x + (c * 8 + b) * scale + sx;
                                int py = y + r * scale + sy;
                                if (px >= 0 && px < BaseWidth && py >= 0 && py < BaseHeight)
                                    buffer[py * bufW + px] = color;
                            }
                    }
                }
            }
        }
    }
}