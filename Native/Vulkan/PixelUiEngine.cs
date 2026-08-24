namespace GpuT.Agent.Native.Vulkan;

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

    public static readonly Rect BtnStartStop = new(35, 75, 195, 38);
    public static readonly Rect Btn10s = new(240, 75, 60, 38);
    public static readonly Rect Btn30s = new(308, 75, 60, 38);
    public static readonly Rect Btn60s = new(376, 75, 60, 38);
    public static readonly Rect BtnUnlimited = new(444, 75, 105, 38);
    
    public static readonly Rect InputCustom = new(560, 75, 160, 38);
    public static readonly Rect BtnMinus = new(730, 75, 45, 38);
    public static readonly Rect BtnPlus = new(783, 75, 45, 38);

    public static string FormatGpuName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Generic GPU";
        raw = raw.Trim();

        bool isZink = false;
        if (raw.StartsWith("zink", StringComparison.OrdinalIgnoreCase))
        {
            isZink = true;
            int firstP = raw.IndexOf('(');
            int lastP = raw.LastIndexOf(')');
            if (firstP >= 0 && lastP > firstP)
            {
                raw = raw.Substring(firstP + 1, lastP - firstP - 1).Trim();
            }
        }

        raw = raw.Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("(r)", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("(tm)", "", StringComparison.OrdinalIgnoreCase);

        int lastParen = raw.LastIndexOf('(');
        if (lastParen > 3)
        {
            raw = raw.Substring(0, lastParen).Trim();
        }

        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        raw = raw.Trim();

        if (isZink) raw = $"Zink: {raw}";
        if (raw.Length > 44) raw = raw.Substring(0, 41) + "...";
        return raw;
    }

    public static unsafe void Render(
        uint* buffer, int width, int height,
        string apiTitle, string gpuName, bool isBenchmarking, int durationSec,
        string customText, bool isCustomFocused,
        double elapsedSec, double dps, double tflops,
        string? hwSensorStr,
        ThemePalette theme,
        int mouseX, int mouseY, float animTime)
    {
        // 1. Процедурный фон с точной палитрой темы
        RenderGpuZBackground(buffer, width, height, isBenchmarking ? animTime : 0.0f, isBenchmarking, theme);

        uint accentColor = GetThemeAccent(theme);
        uint activeBtnBg = GetThemeActiveButtonBg(theme);

        // 2. Верхняя стеклянная панель
        FillRectAlpha(buffer, width, 0, 0, width, 55, 0xDD0D1117);
        DrawLine(buffer, width, 0, 55, width, 55, 0xFF30363D);
        DrawString(buffer, width, 25, 12, $"GPU-T RENDER TEST  |  {apiTitle.ToUpperInvariant()}", 0xFFE6EDF3, 2);
        
        string cleanName = FormatGpuName(gpuName);
        DrawString(buffer, width, 25, 34, $"DEVICE: {cleanName}   •   STATUS: {(isBenchmarking ? "100% STRESS RUNNING" : "IDLE / STANDBY")}", isBenchmarking ? 0xFF3FB950 : accentColor, 1);

        // 3. Кнопка START / STOP
        bool hoverStart = BtnStartStop.Contains(mouseX, mouseY);
        uint startBg = isBenchmarking
            ? (hoverStart ? 0xFFDA3633 : 0xFFA40E26)
            : (hoverStart ? 0xFF2EA043 : 0xFF238636);
        uint startBorder = isBenchmarking ? 0xFFF85149 : 0xFF3FB950;

        FillRectAlpha(buffer, width, BtnStartStop.X, BtnStartStop.Y, BtnStartStop.W, BtnStartStop.H, startBg);
        DrawRect(buffer, width, BtnStartStop.X, BtnStartStop.Y, BtnStartStop.W, BtnStartStop.H, startBorder);
        string startLabel = isBenchmarking ? "⏹  STOP" : "▶  START";
        DrawString(buffer, width, BtnStartStop.X + 35, BtnStartStop.Y + 12, startLabel, 0xFFFFFFFF, 1);

        // Пресеты времени
        bool isPreset = durationSec is 10 or 30 or 60 or 0 && !isCustomFocused;
        DrawPresetButton(buffer, width, Btn10s, "10S", durationSec == 10 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, Btn30s, "30S", durationSec == 30 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, Btn60s, "60S", durationSec == 60 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, BtnUnlimited, "UNLIMITED", durationSec == 0 && isPreset, mouseX, mouseY, accentColor, activeBtnBg);

        // Поле ввода
        bool isCustomActive = isCustomFocused || (!isPreset && durationSec > 0);
        uint inputBg = isCustomFocused ? 0xEE161B22 : (isCustomActive ? activeBtnBg : 0xAA21262D);
        uint inputBorder = isCustomFocused ? accentColor : (isCustomActive ? accentColor : 0xFF30363D);

        FillRectAlpha(buffer, width, InputCustom.X, InputCustom.Y, InputCustom.W, InputCustom.H, inputBg);
        DrawRect(buffer, width, InputCustom.X, InputCustom.Y, InputCustom.W, InputCustom.H, inputBorder);

        bool cursorVisible = isCustomFocused && ((int)(animTime * 4) % 2 == 0);
        string inputDisplay = $"SEC: {(string.IsNullOrEmpty(customText) ? durationSec.ToString() : customText)}{(cursorVisible ? "_" : " ")}";
        DrawString(buffer, width, InputCustom.X + 15, InputCustom.Y + 12, inputDisplay, isCustomActive ? 0xFFFFFFFF : 0xFFC9D1D9, 1);

        DrawPresetButton(buffer, width, BtnMinus, "-", false, mouseX, mouseY, accentColor, activeBtnBg);
        DrawPresetButton(buffer, width, BtnPlus, "+", false, mouseX, mouseY, accentColor, activeBtnBg);

        // 4. Панель телеметрии
        bool hasHwSensor = !string.IsNullOrEmpty(hwSensorStr);
        int hudH = hasHwSensor ? 115 : 95;
        int hudY = hasHwSensor ? 400 : 420;

        FillRectAlpha(buffer, width, 35, hudY, 830, hudH, 0xEE0D1117);
        DrawRect(buffer, width, 35, hudY, 830, hudH, isBenchmarking ? accentColor : 0xFF30363D);

        string durStr = durationSec == 0 ? $"{elapsedSec:F1}s / Unlimited" : $"{elapsedSec:F1}s / {durationSec}s";
        DrawString(buffer, width, 55, hudY + 15, $"TARGET DURATION: {durStr}", 0xFFF0F6FC, 1);
        DrawString(buffer, width, 55, hudY + 35, $"COMPUTE LOAD: ~{tflops:F2} TFLOPS  |  RATE: {dps:F0} Dispatches/s", isBenchmarking ? 0xFFE3B341 : 0xFF8B949E, 1);
        
        if (hasHwSensor)
        {
            DrawString(buffer, width, 55, hudY + 55, $"HW SENSOR (VK_KHR_PERF): {hwSensorStr}", accentColor, 1);
        }
        else
        {
            DrawString(buffer, width, 55, hudY + 55, "COMPUTE PIPELINE: 100% Saturation (VSync OFF)", isBenchmarking ? accentColor : 0xFF8B949E, 1);
        }

        if (durationSec > 0 && isBenchmarking)
        {
            float progress = Math.Clamp((float)(elapsedSec / durationSec), 0f, 1f);
            int barY = hudY + (hasHwSensor ? 85 : 65);
            FillRectAlpha(buffer, width, 55, barY, 790, 6, 0xFF21262D);
            FillRectAlpha(buffer, width, 55, barY, (int)(790 * progress), 6, accentColor);
        }

        DrawString(buffer, width, 35, 525, "Click buttons to control benchmark. Press ESC or close window to exit.", 0xFF6E7681, 1);
    }

    private static unsafe void RenderGpuZBackground(uint* buffer, int width, int height, float time, bool isStress, ThemePalette theme)
    {
        float invW = 1.0f / width;
        float invH = 1.0f / height;

        for (int y = 0; y < height; y++)
        {
            float ny = (y * invH) * 2.0f - 1.0f;
            int rowOffset = y * width;

            for (int x = 0; x < width; x++)
            {
                float nx = (x * invW) * 2.0f - 1.0f;

                float wave1 = MathF.Sin(nx * 4.0f + time * 1.8f);
                float wave2 = MathF.Cos(ny * 4.0f - time * 1.5f);
                float wave3 = MathF.Sin((nx * 0.7f + ny * 0.7f) * 6.0f + time * 2.2f);
                float waveSum = (wave1 + wave2 + wave3) * 0.333f;
                float norm = waveSum * 0.5f + 0.5f;

                byte r = 0, g = 0, b = 0;

                if (isStress)
                {
                    switch (theme)
                    {
                        // 1. VULKAN: Огненно-красный, пылающий оранжевый
                        case ThemePalette.Vulkan:
                            r = (byte)Math.Clamp((int)(200 + 55 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(40 + 110 * (MathF.Sin(time * 1.5f + nx * 2.0f) * 0.5f + 0.5f) * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(10 + 30 * (1.0f - norm)), 0, 255);
                            break;

                        // 2. OPENGL & ZINK: Синий кобальт, неоновый циан
                        case ThemePalette.OpenGL:
                        case ThemePalette.Dxvk:
                            r = (byte)Math.Clamp((int)(15 + 45 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(60 + 130 * (MathF.Sin(time * 1.5f + nx * 2.0f) * 0.5f + 0.5f)), 0, 255);
                            b = (byte)Math.Clamp((int)(180 + 75 * norm), 0, 255);
                            break;

                        // 3. OPENGL ES & ZINK ES: Неоновая маджента, пурпурный, розовый
                        case ThemePalette.OpenGLES:
                            r = (byte)Math.Clamp((int)(180 + 75 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(20 + 55 * (MathF.Sin(time * 2.0f + ny) * 0.5f + 0.5f)), 0, 255);
                            b = (byte)Math.Clamp((int)(160 + 95 * (MathF.Cos(time * 1.5f - nx) * 0.5f + 0.5f)), 0, 255);
                            break;

                        // 4. RUSTICL (Mesa Rust): Фирменный ржаво-медный, бронзовый и янтарь (Rust Orange)
                        case ThemePalette.Rusticl:
                            r = (byte)Math.Clamp((int)(190 + 65 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(65 + 85 * (MathF.Sin(time * 1.6f + nx * 2.2f) * 0.5f + 0.5f)), 0, 255);
                            b = (byte)Math.Clamp((int)(15 + 35 * (1.0f - norm)), 0, 255);
                            break;

                        // 5. OPENCL: Чистая морская волна (Teal), аквамарин и холодный изумруд
                        case ThemePalette.OpenCL:
                            r = (byte)Math.Clamp((int)(15 + 50 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(150 + 105 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(140 + 115 * (MathF.Sin(time * 1.4f + ny * 1.8f) * 0.5f + 0.5f)), 0, 255);
                            break;

                        // 6. CUDA: Лаймово-зеленый NVIDIA
                        case ThemePalette.Cuda:
                            r = (byte)Math.Clamp((int)(10 + 40 * (1.0f - norm)), 0, 255);
                            g = (byte)Math.Clamp((int)(160 + 95 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(20 + 50 * norm), 0, 255);
                            break;

                        // 7. ROCm: Рубиновый красный AMD
                        case ThemePalette.Rocm:
                            r = (byte)Math.Clamp((int)(210 + 45 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(15 + 35 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(25 + 45 * norm), 0, 255);
                            break;

                        // 8. OneAPI: Электрический голубой Intel
                        case ThemePalette.OneApi:
                            r = (byte)Math.Clamp((int)(10 + 40 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(120 + 115 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(210 + 45 * norm), 0, 255);
                            break;

                        // 9. VKD3D: Сиреневый и стальной Valve
                        case ThemePalette.Vkd3d:
                            r = (byte)Math.Clamp((int)(140 + 80 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(80 + 70 * norm), 0, 255);
                            b = (byte)Math.Clamp((int)(150 + 90 * norm), 0, 255);
                            break;

                        // 10. WineD3D: Винный каберне
                        case ThemePalette.WineD3d:
                            r = (byte)Math.Clamp((int)(170 + 75 * norm), 0, 255);
                            g = (byte)Math.Clamp((int)(15 + 30 * (1.0f - norm)), 0, 255);
                            b = (byte)Math.Clamp((int)(40 + 50 * norm), 0, 255);
                            break;
                    }
                }
                else
                {
                    // Фоновый градиент в режиме ожидания (Idle)
                    float baseGrad = (ny * 0.5f + 0.5f);
                    switch (theme)
                    {
                        case ThemePalette.Vulkan:
                            r = (byte)(28 + 22 * baseGrad); g = (byte)(12 + 10 * baseGrad); b = (byte)(12 + 10 * baseGrad);
                            break;
                        case ThemePalette.OpenGLES:
                            r = (byte)(24 + 20 * baseGrad); g = (byte)(12 + 10 * baseGrad); b = (byte)(28 + 25 * baseGrad);
                            break;
                        case ThemePalette.Rusticl:
                            r = (byte)(30 + 24 * baseGrad); g = (byte)(16 + 12 * baseGrad); b = (byte)(12 + 8 * baseGrad);
                            break;
                        case ThemePalette.OpenCL:
                            r = (byte)(10 + 8 * baseGrad); g = (byte)(24 + 20 * baseGrad); b = (byte)(26 + 22 * baseGrad);
                            break;
                        case ThemePalette.Cuda:
                            r = (byte)(12 + 10 * baseGrad); g = (byte)(26 + 24 * baseGrad); b = (byte)(14 + 10 * baseGrad);
                            break;
                        case ThemePalette.Rocm:
                        case ThemePalette.WineD3d:
                            r = (byte)(30 + 22 * baseGrad); g = (byte)(12 + 10 * baseGrad); b = (byte)(16 + 12 * baseGrad);
                            break;
                        default: // OpenGL / Intel / Zink (Тёмно-синий)
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
        ThemePalette.Vulkan   => 0xFFFF5722, // Огненно-оранжевый (Vulkan)
        ThemePalette.OpenGLES => 0xFFFF4081, // Неоново-розовый (OpenGL ES)
        ThemePalette.Rusticl  => 0xFFFF7043, // Медно-ржавый оранжевый (Rusticl)
        ThemePalette.OpenCL   => 0xFF2DD4BF, // Аквамарин / Teal (OpenCL)
        ThemePalette.Cuda     => 0xFF00E676, // Лаймово-зеленый (NVIDIA)
        ThemePalette.Rocm     => 0xFFFF1744, // Красный (AMD)
        ThemePalette.OneApi   => 0xFF00B0FF, // Голубой (Intel)
        ThemePalette.Vkd3d    => 0xFFE040FB, // Сиреневый (Valve)
        ThemePalette.WineD3d  => 0xFFFF5252, // Винный (Wine)
        _                     => 0xFF2979FF  // Кобальтово-синий (OpenGL)
    };

    private static uint GetThemeActiveButtonBg(ThemePalette theme) => theme switch
    {
        ThemePalette.Vulkan   => 0xEEB7300D,
        ThemePalette.OpenGLES => 0xEEA0144F,
        ThemePalette.Rusticl  => 0xEEBF360C,
        ThemePalette.OpenCL   => 0xEE00695C,
        ThemePalette.Cuda     => 0xEE007E33,
        ThemePalette.Rocm     => 0xEEB71C1C,
        ThemePalette.OneApi   => 0xEE01579B,
        ThemePalette.WineD3d  => 0xEE880E4F,
        _                     => 0xEE0D47A1
    };

    private static unsafe void DrawPresetButton(uint* buffer, int width, Rect rect, string text, bool isSelected, int mx, int my, uint accentCol, uint activeBg)
    {
        bool hover = rect.Contains(mx, my);
        uint bg = isSelected ? activeBg : (hover ? 0xDD30363D : 0xAA21262D);
        uint border = isSelected ? accentCol : (hover ? 0xFF8B949E : 0xFF30363D);

        FillRectAlpha(buffer, width, rect.X, rect.Y, rect.W, rect.H, bg);
        DrawRect(buffer, width, rect.X, rect.Y, rect.W, rect.H, border);
        DrawString(buffer, width, rect.X + (rect.W - text.Length * 8) / 2, rect.Y + 12, text, isSelected ? 0xFFFFFFFF : 0xFFC9D1D9, 1);
    }

    private static unsafe void FillRectAlpha(uint* buffer, int bufW, int x, int y, int w, int h, uint col)
    {
        byte a = (byte)(col >> 24);
        if (a == 255)
        {
            for (int j = y; j < y + h && j < BaseHeight; j++)
                for (int i = x; i < x + w && i < BaseWidth; i++)
                    buffer[j * bufW + i] = col;
            return;
        }

        float alpha = a / 255.0f;
        float invA = 1.0f - alpha;
        uint srcR = (col >> 16) & 0xFF, srcG = (col >> 8) & 0xFF, srcB = col & 0xFF;

        for (int j = y; j < y + h && j < BaseHeight; j++)
        {
            int row = j * bufW;
            for (int i = x; i < x + w && i < BaseWidth; i++)
            {
                uint dst = buffer[row + i];
                uint dr = (dst >> 16) & 0xFF, dg = (dst >> 8) & 0xFF, db = dst & 0xFF;
                uint outR = (uint)(srcR * alpha + dr * invA);
                uint outG = (uint)(srcG * alpha + dg * invA);
                uint outB = (uint)(srcB * alpha + db * invA);
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
            char ch = char.ToUpperInvariant(text[c]);
            byte[] glyph = GetGlyph(ch);
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
                                if (px >= 0 && px < BaseWidth && py >= 0 && py < BaseHeight) buffer[py * bufW + px] = color;
                            }
                    }
                }
            }
        }
    }

    private static byte[] GetGlyph(char c) => c switch
    {
        'A' => [0x18, 0x3C, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00],
        'B' => [0x7C, 0x66, 0x7C, 0x66, 0x66, 0x66, 0x7C, 0x00],
        'C' => [0x3C, 0x66, 0x60, 0x60, 0x60, 0x66, 0x3C, 0x00],
        'D' => [0x78, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0x78, 0x00],
        'E' => [0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x7E, 0x00],
        'F' => [0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x60, 0x00],
        'G' => [0x3C, 0x66, 0x60, 0x6E, 0x66, 0x66, 0x3E, 0x00],
        'H' => [0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x66, 0x00],
        'I' => [0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00],
        'J' => [0x1E, 0x0C, 0x0C, 0x0C, 0x0C, 0x6C, 0x38, 0x00],
        'K' => [0x66, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0x66, 0x00],
        'L' => [0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00],
        'M' => [0x63, 0x77, 0x7F, 0x6B, 0x63, 0x63, 0x63, 0x00],
        'N' => [0x66, 0x76, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x00],
        'O' => [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
        'P' => [0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0x00],
        'Q' => [0x3C, 0x66, 0x66, 0x66, 0x6A, 0x6C, 0x36, 0x00],
        'R' => [0x7C, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0x66, 0x00],
        'S' => [0x3C, 0x66, 0x30, 0x1C, 0x06, 0x66, 0x3C, 0x00],
        'T' => [0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00],
        'U' => [0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
        'V' => [0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x00],
        'W' => [0x63, 0x63, 0x63, 0x6B, 0x7F, 0x77, 0x63, 0x00],
        'X' => [0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0x00],
        'Y' => [0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 0x00],
        'Z' => [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 0x00],
        '0' => [0x3C, 0x66, 0x6E, 0x76, 0x66, 0x66, 0x3C, 0x00],
        '1' => [0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00],
        '2' => [0x3C, 0x66, 0x06, 0x1C, 0x30, 0x60, 0x7E, 0x00],
        '3' => [0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00],
        '4' => [0x0C, 0x1C, 0x34, 0x64, 0x7E, 0x04, 0x04, 0x00],
        '5' => [0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00],
        '6' => [0x3C, 0x66, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0x00],
        '7' => [0x7E, 0x06, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00],
        '8' => [0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00],
        '9' => [0x3C, 0x66, 0x66, 0x3E, 0x06, 0x66, 0x3C, 0x00],
        '+' => [0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00],
        '-' => [0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00],
        ':' => [0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00],
        '.' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00],
        '/' => [0x02, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x40, 0x00],
        '|' => [0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00],
        '_' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00],
        '[' => [0x1E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x1E, 0x00],
        ']' => [0x78, 0x18, 0x18, 0x18, 0x18, 0x18, 0x78, 0x00],
        '(' => [0x0C, 0x18, 0x30, 0x30, 0x30, 0x18, 0x0C, 0x00],
        ')' => [0x30, 0x18, 0x0C, 0x0C, 0x0C, 0x18, 0x30, 0x00],
        '%' => [0x62, 0x64, 0x08, 0x10, 0x26, 0x46, 0x00, 0x00],
        '>' => [0x60, 0x30, 0x18, 0x0C, 0x18, 0x30, 0x60, 0x00],
        '=' => [0x00, 0x7E, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00],
        '~' => [0x00, 0x36, 0x5B, 0x00, 0x00, 0x00, 0x00, 0x00],
        '•' => [0x00, 0x18, 0x3C, 0x3C, 0x18, 0x00, 0x00, 0x00],
        _   => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
    };
}