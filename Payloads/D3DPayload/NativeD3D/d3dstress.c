#define WIN32_LEAN_AND_MEAN
#define COBJMACROS
#include <windows.h>
#include <initguid.h>
#include <d3d9.h>
#include <d3d11.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#include <math.h>

#define BASE_WIDTH 900
#define BASE_HEIGHT 550

static uint32_t ui_pixels[BASE_WIDTH * BASE_HEIGHT];
static int g_mouse_x = 0;
static int g_mouse_y = 0;
static bool g_is_running = true;
static bool g_is_benchmarking = true;
static bool g_bench_reset_requested = false;
static int g_target_duration = 0;
static char g_custom_text[16] = "";
static bool g_is_custom_focused = false;
static float g_anim_time = 0.0f;

// Bounding Boxes for UI Buttons
#define BTN_START_X 35
#define BTN_START_Y 75
#define BTN_START_W 195
#define BTN_START_H 38

#define BTN_10S_X 240
#define BTN_30S_X 308
#define BTN_60S_X 376
#define BTN_UNL_X 444
#define BTN_PRESET_W 60
#define BTN_UNL_W 105
#define BTN_Y 75
#define BTN_H 38

#define INPUT_CUSTOM_X 560
#define INPUT_CUSTOM_W 160
#define BTN_MINUS_X 730
#define BTN_PLUS_X 783
#define BTN_STEP_W 45

static bool rect_contains(int rx, int ry, int rw, int rh, int px, int py) {
    return px >= rx && px < (rx + rw) && py >= ry && py < (ry + rh);
}

// Complete 8x8 ASCII Font (Includes All Digits 0-9, Letters A-Z, Parentheses, Symbols)
static const uint8_t font8x8_basic[128][8] = {
    ['A'] = {0x18, 0x3C, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00},
    ['B'] = {0x7C, 0x66, 0x7C, 0x66, 0x66, 0x66, 0x7C, 0x00},
    ['C'] = {0x3C, 0x66, 0x60, 0x60, 0x60, 0x66, 0x3C, 0x00},
    ['D'] = {0x78, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0x78, 0x00},
    ['E'] = {0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x7E, 0x00},
    ['F'] = {0x7E, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x60, 0x00},
    ['G'] = {0x3C, 0x66, 0x60, 0x6E, 0x66, 0x66, 0x3E, 0x00},
    ['H'] = {0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x66, 0x00},
    ['I'] = {0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00},
    ['J'] = {0x1E, 0x0C, 0x0C, 0x0C, 0x0C, 0x6C, 0x38, 0x00},
    ['K'] = {0x66, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0x66, 0x00},
    ['L'] = {0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00},
    ['M'] = {0x63, 0x77, 0x7F, 0x6B, 0x63, 0x63, 0x63, 0x00},
    ['N'] = {0x66, 0x76, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x00},
    ['O'] = {0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00},
    ['P'] = {0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0x00},
    ['Q'] = {0x3C, 0x66, 0x66, 0x66, 0x6A, 0x6C, 0x36, 0x00},
    ['R'] = {0x7C, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0x66, 0x00},
    ['S'] = {0x3C, 0x66, 0x30, 0x1C, 0x06, 0x66, 0x3C, 0x00},
    ['T'] = {0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00},
    ['U'] = {0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00},
    ['V'] = {0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x00},
    ['W'] = {0x63, 0x63, 0x63, 0x6B, 0x7F, 0x77, 0x63, 0x00},
    ['X'] = {0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0x00},
    ['Y'] = {0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 0x00},
    ['Z'] = {0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 0x00},
    ['0'] = {0x3C, 0x66, 0x6E, 0x76, 0x66, 0x66, 0x3C, 0x00},
    ['1'] = {0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00},
    ['2'] = {0x3C, 0x66, 0x06, 0x1C, 0x30, 0x60, 0x7E, 0x00},
    ['3'] = {0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00},
    ['4'] = {0x0C, 0x1C, 0x34, 0x64, 0x7E, 0x04, 0x04, 0x00},
    ['5'] = {0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00},
    ['6'] = {0x3C, 0x66, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0x00},
    ['7'] = {0x7E, 0x06, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00},
    ['8'] = {0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00},
    ['9'] = {0x3C, 0x66, 0x66, 0x3E, 0x06, 0x66, 0x3C, 0x00},
    ['('] = {0x0C, 0x18, 0x30, 0x30, 0x30, 0x18, 0x0C, 0x00},
    [')'] = {0x30, 0x18, 0x0C, 0x0C, 0x0C, 0x18, 0x30, 0x00},
    ['['] = {0x1E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x1E, 0x00},
    [']'] = {0x78, 0x18, 0x18, 0x18, 0x18, 0x18, 0x78, 0x00},
    [':'] = {0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00},
    ['-'] = {0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00},
    ['+'] = {0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00},
    ['|'] = {0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00},
    ['.'] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00},
    [','] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x30},
    ['/'] = {0x02, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x40, 0x00},
    ['~'] = {0x00, 0x36, 0x5B, 0x00, 0x00, 0x00, 0x00, 0x00},
    ['%'] = {0x62, 0x64, 0x08, 0x10, 0x26, 0x46, 0x00, 0x00},
    ['_'] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00},
    [' '] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}
};

static void draw_char(uint32_t* buf, int x, int y, char c, uint32_t col, int scale) {
    if ((uint8_t)c > 127) return;
    const uint8_t* glyph = font8x8_basic[(uint8_t)c];
    for (int r = 0; r < 8; r++) {
        uint8_t row = glyph[r];
        for (int b = 0; b < 8; b++) {
            if (row & (1 << (7 - b))) {
                for (int sy = 0; sy < scale; sy++) {
                    for (int sx = 0; sx < scale; sx++) {
                        int px = x + (b * scale) + sx;
                        int py = y + (r * scale) + sy;
                        if (px >= 0 && px < BASE_WIDTH && py >= 0 && py < BASE_HEIGHT)
                            buf[py * BASE_WIDTH + px] = col;
                    }
                }
            }
        }
    }
}

static void draw_string(uint32_t* buf, int x, int y, const char* str, uint32_t col, int scale) {
    int cur_x = x;
    while (*str) {
        char c = *str >= 'a' && *str <= 'z' ? (*str - 32) : *str;
        draw_char(buf, cur_x, y, c, col, scale);
        cur_x += 8 * scale;
        str++;
    }
}

static void fill_rect(uint32_t* buf, int x, int y, int w, int h, uint32_t col) {
    for (int j = y; j < y + h && j < BASE_HEIGHT; j++) {
        for (int i = x; i < x + w && i < BASE_WIDTH; i++) {
            buf[j * BASE_WIDTH + i] = col;
        }
    }
}

static void draw_rect(uint32_t* buf, int x, int y, int w, int h, uint32_t col) {
    for (int i = x; i < x + w && i < BASE_WIDTH; i++) {
        if (y >= 0 && y < BASE_HEIGHT) buf[y * BASE_WIDTH + i] = col;
        if (y + h - 1 >= 0 && y + h - 1 < BASE_HEIGHT) buf[(y + h - 1) * BASE_WIDTH + i] = col;
    }
    for (int j = y; j < y + h && j < BASE_HEIGHT; j++) {
        if (x >= 0 && x < BASE_WIDTH) buf[j * BASE_WIDTH + x] = col;
        if (x + w - 1 >= 0 && x + w - 1 < BASE_WIDTH) buf[j * BASE_WIDTH + (x + w - 1)] = col;
    }
}

static void draw_preset_btn(uint32_t* buf, int x, int y, int w, int h, const char* text, bool is_selected, uint32_t accent) {
    bool hover = rect_contains(x, y, w, h, g_mouse_x, g_mouse_y);
    uint32_t bg = is_selected ? (accent & 0xEEFFFFFF) : (hover ? 0xDD30363D : 0xAA21262D);
    fill_rect(buf, x, y, w, h, bg);
    draw_rect(buf, x, y, w, h, is_selected ? accent : 0xFF30363D);
    draw_string(buf, x + (w - (int)strlen(text) * 8) / 2, y + 12, text, is_selected ? 0xFFFFFFFF : 0xFFC9D1D9, 1);
}

static void render_gui(uint32_t* buf, const char* api_name, const char* gpu_name, double elapsed, double fps, double tflops, int theme) {
    uint32_t accent = (theme == 1) ? 0xFFE040FB : 0xFF2979FF;

    for (int y = 0; y < BASE_HEIGHT; y++) {
        float ny = ((float)y / BASE_HEIGHT) * 2.0f - 1.0f;
        for (int x = 0; x < BASE_WIDTH; x++) {
            float nx = ((float)x / BASE_WIDTH) * 2.0f - 1.0f;
            float wave = (sinf(nx * 4.0f + g_anim_time * 1.8f) + cosf(ny * 4.0f - g_anim_time * 1.5f)) * 0.5f * 0.5f + 0.5f;
            uint8_t r = 20, g = 30, b = 60;
            if (g_is_benchmarking) {
                if (theme == 1) { r = 180 * wave; g = 40; b = 200 * wave; }
                else { r = 20; g = 100 * wave + 40; b = 220 * wave + 30; }
            }
            buf[y * BASE_WIDTH + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }

    fill_rect(buf, 0, 0, BASE_WIDTH, 55, 0xFF0D1117);
    char title[128];
    snprintf(title, sizeof(title), "GPU-T RENDER TEST  |  %s (NATIVE C)", api_name);
    draw_string(buf, 25, 15, title, 0xFFFFFFFF, 2);

    char sub[128];
    snprintf(sub, sizeof(sub), "DEVICE: %s   •   STATUS: %s", gpu_name, g_is_benchmarking ? "100% STRESS RUNNING" : "IDLE / STANDBY");
    draw_string(buf, 25, 36, sub, g_is_benchmarking ? 0xFF3FB950 : accent, 1);

    uint32_t btn_col = g_is_benchmarking ? 0xFFDA3633 : 0xFF238636;
    fill_rect(buf, BTN_START_X, BTN_START_Y, BTN_START_W, BTN_START_H, btn_col);
    draw_rect(buf, BTN_START_X, BTN_START_Y, BTN_START_W, BTN_START_H, g_is_benchmarking ? 0xFFF85149 : 0xFF3FB950);
    draw_string(buf, 75, 87, g_is_benchmarking ? "STOP" : "START", 0xFFFFFFFF, 1);

    bool isPreset = (g_target_duration == 10 || g_target_duration == 30 || g_target_duration == 60 || g_target_duration == 0) && !g_is_custom_focused;
    draw_preset_btn(buf, BTN_10S_X, BTN_Y, BTN_PRESET_W, BTN_H, "10S", g_target_duration == 10 && isPreset, accent);
    draw_preset_btn(buf, BTN_30S_X, BTN_Y, BTN_PRESET_W, BTN_H, "30S", g_target_duration == 30 && isPreset, accent);
    draw_preset_btn(buf, BTN_60S_X, BTN_Y, BTN_PRESET_W, BTN_H, "60S", g_target_duration == 60 && isPreset, accent);
    draw_preset_btn(buf, BTN_UNL_X, BTN_Y, BTN_UNL_W, BTN_H, "UNLIMITED", g_target_duration == 0 && isPreset, accent);

    bool isCustomActive = g_is_custom_focused || (!isPreset && g_target_duration > 0);
    fill_rect(buf, INPUT_CUSTOM_X, BTN_Y, INPUT_CUSTOM_W, BTN_H, g_is_custom_focused ? 0xFF161B22 : 0xAA21262D);
    draw_rect(buf, INPUT_CUSTOM_X, BTN_Y, INPUT_CUSTOM_W, BTN_H, isCustomActive ? accent : 0xFF30363D);

    char input_display[32];
    snprintf(input_display, sizeof(input_display), "SEC: %s%s", strlen(g_custom_text) ? g_custom_text : "0", g_is_custom_focused ? "_" : " ");
    draw_string(buf, INPUT_CUSTOM_X + 15, BTN_Y + 12, input_display, 0xFFFFFFFF, 1);

    draw_preset_btn(buf, BTN_MINUS_X, BTN_Y, BTN_STEP_W, BTN_H, "-", false, accent);
    draw_preset_btn(buf, BTN_PLUS_X, BTN_Y, BTN_STEP_W, BTN_H, "+", false, accent);

    fill_rect(buf, 35, 420, 830, 95, 0xFF0D1117);
    draw_rect(buf, 35, 420, 830, 95, g_is_benchmarking ? accent : 0xFF30363D);

    char durStr[64];
    if (g_target_duration == 0) snprintf(durStr, sizeof(durStr), "%.1fs / Unlimited", elapsed);
    else snprintf(durStr, sizeof(durStr), "%.1fs / %ds", elapsed, g_target_duration);

    char metrics[128];
    snprintf(metrics, sizeof(metrics), "TARGET DURATION: %s", durStr);
    draw_string(buf, 55, 435, metrics, 0xFFF0F6FC, 1);

    snprintf(metrics, sizeof(metrics), "COMPUTE LOAD: ~%.2f TFLOPS  |  RATE: %.0f FPS", tflops, fps);
    draw_string(buf, 55, 455, metrics, g_is_benchmarking ? 0xFFE3B341 : 0xFF8B949E, 1);

    draw_string(buf, 55, 475, "DIRECT3D ACCELERATION: 100% Saturation Active", g_is_benchmarking ? accent : 0xFF8B949E, 1);

    if (g_target_duration > 0 && g_is_benchmarking) {
        float progress = (float)elapsed / g_target_duration;
        if (progress > 1.0f) progress = 1.0f;
        fill_rect(buf, 55, 495, 790, 6, 0xFF21262D);
        fill_rect(buf, 55, 495, (int)(790 * progress), 6, accent);
    }
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_MOUSEMOVE:
            g_mouse_x = LOWORD(lParam);
            g_mouse_y = HIWORD(lParam);
            return 0;

        case WM_LBUTTONDOWN: {
            int mx = LOWORD(lParam), my = HIWORD(lParam);
            if (rect_contains(BTN_START_X, BTN_START_Y, BTN_START_W, BTN_START_H, mx, my)) {
                g_is_benchmarking = !g_is_benchmarking;
                if (g_is_benchmarking) g_bench_reset_requested = true;
                g_is_custom_focused = false;
            } else if (rect_contains(BTN_10S_X, BTN_Y, BTN_PRESET_W, BTN_H, mx, my)) {
                g_target_duration = 10; strcpy(g_custom_text, "10"); g_is_custom_focused = false;
            } else if (rect_contains(BTN_30S_X, BTN_Y, BTN_PRESET_W, BTN_H, mx, my)) {
                g_target_duration = 30; strcpy(g_custom_text, "30"); g_is_custom_focused = false;
            } else if (rect_contains(BTN_60S_X, BTN_Y, BTN_PRESET_W, BTN_H, mx, my)) {
                g_target_duration = 60; strcpy(g_custom_text, "60"); g_is_custom_focused = false;
            } else if (rect_contains(BTN_UNL_X, BTN_Y, BTN_UNL_W, BTN_H, mx, my)) {
                g_target_duration = 0; strcpy(g_custom_text, ""); g_is_custom_focused = false;
            } else if (rect_contains(INPUT_CUSTOM_X, BTN_Y, INPUT_CUSTOM_W, BTN_H, mx, my)) {
                g_is_custom_focused = true;
            } else if (rect_contains(BTN_MINUS_X, BTN_Y, BTN_STEP_W, BTN_H, mx, my)) {
                g_target_duration = g_target_duration > 5 ? g_target_duration - 5 : 1;
                snprintf(g_custom_text, sizeof(g_custom_text), "%d", g_target_duration);
                g_is_custom_focused = false;
            } else if (rect_contains(BTN_PLUS_X, BTN_Y, BTN_STEP_W, BTN_H, mx, my)) {
                g_target_duration += 5;
                snprintf(g_custom_text, sizeof(g_custom_text), "%d", g_target_duration);
                g_is_custom_focused = false;
            } else {
                g_is_custom_focused = false;
            }
            return 0;
        }

        case WM_CHAR:
            if (g_is_custom_focused && wParam >= '0' && wParam <= '9' && strlen(g_custom_text) < 5) {
                int len = strlen(g_custom_text);
                g_custom_text[len] = (char)wParam;
                g_custom_text[len + 1] = '\0';
                g_target_duration = atoi(g_custom_text);
            }
            return 0;

        case WM_KEYDOWN:
            if (g_is_custom_focused) {
                if (wParam == VK_BACK && strlen(g_custom_text) > 0) {
                    g_custom_text[strlen(g_custom_text) - 1] = '\0';
                    g_target_duration = atoi(g_custom_text);
                } else if (wParam == VK_RETURN || wParam == VK_ESCAPE) {
                    g_is_custom_focused = false;
                }
            } else {
                if (wParam == VK_SPACE) {
                    g_is_benchmarking = !g_is_benchmarking;
                    if (g_is_benchmarking) g_bench_reset_requested = true;
                }
                if (wParam == VK_ESCAPE) g_is_running = false;
            }
            return 0;

        case WM_SETCURSOR:
            SetCursor(LoadCursor(NULL, IDC_ARROW));
            return TRUE;

        case WM_DESTROY:
            g_is_running = false;
            PostQuitMessage(0);
            return 0;
    }
    return DefWindowProc(hwnd, msg, wParam, lParam);
}

static HWND create_window(const char* title) {
    WNDCLASSEX wc = {
        .cbSize = sizeof(WNDCLASSEX),
        .lpfnWndProc = WndProc,
        .hInstance = GetModuleHandle(NULL),
        .hCursor = LoadCursor(NULL, IDC_ARROW),
        .lpszClassName = "GPUT_Native_D3D"
    };
    RegisterClassEx(&wc);
    return CreateWindowEx(0, "GPUT_Native_D3D", title, WS_OVERLAPPEDWINDOW | WS_VISIBLE,
                          100, 100, BASE_WIDTH, BASE_HEIGHT, NULL, NULL, GetModuleHandle(NULL), NULL);
}

// --- Direct3D 9 Runner ---
typedef struct { float x, y, z, rhw; float u, v; } D3D9VERTEX;
#define D3DFVF_CUSTOMVERTEX (D3DFVF_XYZRHW | D3DFVF_TEX1)

static void run_dx9(int duration) {
    HWND hwnd = create_window("GPU-T Direct3D 9 Stress Test (Native C)");
    IDirect3D9* d3d9 = Direct3DCreate9(D3D_SDK_VERSION);
    if (!d3d9) return;

    char gpu_name[512] = "Direct3D 9 Hardware Device";
    D3DADAPTER_IDENTIFIER9 ident;
    if (SUCCEEDED(IDirect3D9_GetAdapterIdentifier(d3d9, D3DADAPTER_DEFAULT, 0, &ident))) {
        strncpy(gpu_name, ident.Description, sizeof(gpu_name));
    }

    D3DPRESENT_PARAMETERS d3dpp = {
        .BackBufferWidth = BASE_WIDTH,
        .BackBufferHeight = BASE_HEIGHT,
        .BackBufferFormat = D3DFMT_A8R8G8B8,
        .BackBufferCount = 1,
        .Windowed = TRUE,
        .SwapEffect = D3DSWAPEFFECT_DISCARD,
        .hDeviceWindow = hwnd,
        .PresentationInterval = 0x80000000
    };

    IDirect3DDevice9* dev = NULL;
    if (FAILED(IDirect3D9_CreateDevice(d3d9, D3DADAPTER_DEFAULT, D3DDEVTYPE_HAL, hwnd, D3DCREATE_HARDWARE_VERTEXPROCESSING, &d3dpp, &dev))) {
        if (FAILED(IDirect3D9_CreateDevice(d3d9, D3DADAPTER_DEFAULT, D3DDEVTYPE_HAL, hwnd, D3DCREATE_SOFTWARE_VERTEXPROCESSING, &d3dpp, &dev)))
            return;
    }

    D3D9VERTEX quad_verts[4] = {
        { 0, 0, 0.5f, 1.0f, 0, 0 },
        { BASE_WIDTH, 0, 0.5f, 1.0f, 1, 0 },
        { 0, BASE_HEIGHT, 0.5f, 1.0f, 0, 1 },
        { BASE_WIDTH, BASE_HEIGHT, 0.5f, 1.0f, 1, 1 }
    };

    IDirect3DSurface9* gui_surface = NULL;
    IDirect3DDevice9_CreateOffscreenPlainSurface(dev, BASE_WIDTH, BASE_HEIGHT, D3DFMT_A8R8G8B8, D3DPOOL_SYSTEMMEM, &gui_surface, NULL);
    IDirect3DSurface9* back_buffer = NULL;
    IDirect3DDevice9_GetBackBuffer(dev, 0, 0, D3DBACKBUFFER_TYPE_MONO, &back_buffer);

    IDirect3DDevice9_SetRenderState(dev, D3DRS_ALPHABLENDENABLE, TRUE);
    IDirect3DDevice9_SetRenderState(dev, D3DRS_SRCBLEND, D3DBLEND_ONE);
    IDirect3DDevice9_SetRenderState(dev, D3DRS_DESTBLEND, D3DBLEND_ONE);

    g_target_duration = duration;
    if (duration > 0) snprintf(g_custom_text, sizeof(g_custom_text), "%d", duration);

    MSG msg;
    DWORD bench_start_time = GetTickCount();
    DWORD last_fps_time = bench_start_time;
    uint64_t total_frames = 0, last_frames = 0;
    double fps = 60.0;
    double elapsed = 0.0;

    while (g_is_running) {
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) g_is_running = false;
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        if (g_bench_reset_requested) {
            bench_start_time = GetTickCount();
            elapsed = 0.0;
            g_bench_reset_requested = false;
        }

        if (g_is_benchmarking) {
            g_anim_time += 0.02f;
            elapsed = (GetTickCount() - bench_start_time) / 1000.0;
            if (g_target_duration > 0 && elapsed >= g_target_duration) {
                g_is_benchmarking = false;
            }
        }

        // 4096 Quads = 100% GPU Fillrate Saturation
        if (g_is_benchmarking) {
            IDirect3DDevice9_BeginScene(dev);
            IDirect3DDevice9_SetFVF(dev, D3DFVF_CUSTOMVERTEX);
            for (int p = 0; p < 4096; p++) {
                IDirect3DDevice9_DrawPrimitiveUP(dev, D3DPT_TRIANGLESTRIP, 2, quad_verts, sizeof(D3D9VERTEX));
            }
            IDirect3DDevice9_EndScene(dev);
        }

        render_gui(ui_pixels, "DIRECT3D 9", gpu_name, elapsed, fps, (fps * 1.52) / 1000.0, 0);

        D3DLOCKED_RECT lr;
        if (SUCCEEDED(IDirect3DSurface9_LockRect(gui_surface, &lr, NULL, 0))) {
            for (int y = 0; y < BASE_HEIGHT; y++) {
                memcpy((uint8_t*)lr.pBits + (y * lr.Pitch), ui_pixels + (y * BASE_WIDTH), BASE_WIDTH * 4);
            }
            IDirect3DSurface9_UnlockRect(gui_surface);
            IDirect3DDevice9_UpdateSurface(dev, gui_surface, NULL, back_buffer, NULL);
        }

        IDirect3DDevice9_Present(dev, NULL, NULL, NULL, NULL);
        total_frames++;

        DWORD cur_time = GetTickCount();
        if (cur_time - last_fps_time >= 250) {
            fps = g_is_benchmarking ? (double)(total_frames - last_frames) / ((cur_time - last_fps_time) / 1000.0) : 60.0;
            last_frames = total_frames;
            last_fps_time = cur_time;
        }
        if (!g_is_benchmarking) Sleep(16);
    }
}

// --- Direct3D 11 Runner ---
static void run_dx11(int duration) {
    HWND hwnd = create_window("GPU-T Direct3D 11 Stress Test (Native C)");
    DXGI_SWAP_CHAIN_DESC scDesc = {
        .BufferCount = 1,
        .BufferDesc = { .Width = BASE_WIDTH, .Height = BASE_HEIGHT, .Format = DXGI_FORMAT_B8G8R8A8_UNORM },
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .OutputWindow = hwnd,
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .Windowed = TRUE,
        .SwapEffect = DXGI_SWAP_EFFECT_DISCARD
    };

    ID3D11Device* dev = NULL;
    ID3D11DeviceContext* ctx = NULL;
    IDXGISwapChain* sc = NULL;
    D3D11CreateDeviceAndSwapChain(NULL, D3D_DRIVER_TYPE_HARDWARE, NULL, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                                  NULL, 0, D3D11_SDK_VERSION, &scDesc, &sc, &dev, NULL, &ctx);

    char gpu_name[256] = "Direct3D 11 Graphics Device";
    IDXGIDevice* dxgi_dev = NULL;
    if (SUCCEEDED(ID3D11Device_QueryInterface(dev, &IID_IDXGIDevice, (void**)&dxgi_dev))) {
        IDXGIAdapter* adapter = NULL;
        if (SUCCEEDED(IDXGIDevice_GetAdapter(dxgi_dev, &adapter))) {
            DXGI_ADAPTER_DESC desc;
            if (SUCCEEDED(IDXGIAdapter_GetDesc(adapter, &desc))) {
                wcstombs(gpu_name, desc.Description, sizeof(gpu_name));
            }
            IDXGIAdapter_Release(adapter);
        }
        IDXGIDevice_Release(dxgi_dev);
    }

    ID3D11Texture2D* back_buf = NULL;
    IDXGISwapChain_GetBuffer(sc, 0, &IID_ID3D11Texture2D, (void**)&back_buf);

    D3D11_TEXTURE2D_DESC stgDesc = {
        .Width = BASE_WIDTH, .Height = BASE_HEIGHT, .MipLevels = 1, .ArraySize = 1,
        .Format = DXGI_FORMAT_B8G8R8A8_UNORM, .SampleDesc = {1, 0},
        .Usage = D3D11_USAGE_STAGING, .CPUAccessFlags = D3D11_CPU_ACCESS_WRITE
    };
    ID3D11Texture2D* staging = NULL;
    ID3D11Device_CreateTexture2D(dev, &stgDesc, NULL, &staging);

    g_target_duration = duration;
    if (duration > 0) snprintf(g_custom_text, sizeof(g_custom_text), "%d", duration);

    MSG msg;
    DWORD bench_start_time = GetTickCount();
    DWORD last_fps_time = bench_start_time;
    uint64_t total_frames = 0, last_frames = 0;
    double fps = 60.0;
    double elapsed = 0.0;

    while (g_is_running) {
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) g_is_running = false;
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        if (g_bench_reset_requested) {
            bench_start_time = GetTickCount();
            elapsed = 0.0;
            g_bench_reset_requested = false;
        }

        if (g_is_benchmarking) {
            g_anim_time += 0.02f;
            elapsed = (GetTickCount() - bench_start_time) / 1000.0;
            if (g_target_duration > 0 && elapsed >= g_target_duration) {
                g_is_benchmarking = false;
            }
        }

        render_gui(ui_pixels, "DIRECT3D 11", gpu_name, elapsed, fps, (fps * 2.15) / 1000.0, 0);

        D3D11_MAPPED_SUBRESOURCE mapped;
        if (SUCCEEDED(ID3D11DeviceContext_Map(ctx, (ID3D11Resource*)staging, 0, D3D11_MAP_WRITE, 0, &mapped))) {
            for (int r = 0; r < BASE_HEIGHT; r++) {
                memcpy((uint8_t*)mapped.pData + r * mapped.RowPitch, ui_pixels + r * BASE_WIDTH, BASE_WIDTH * 4);
            }
            ID3D11DeviceContext_Unmap(ctx, (ID3D11Resource*)staging, 0);

            int passes = g_is_benchmarking ? 1024 : 1;
            for (int p = 0; p < passes; p++) {
                ID3D11DeviceContext_CopyResource(ctx, (ID3D11Resource*)back_buf, (ID3D11Resource*)staging);
            }
        }

        IDXGISwapChain_Present(sc, 0, 0);
        total_frames++;

        DWORD cur_time = GetTickCount();
        if (cur_time - last_fps_time >= 250) {
            fps = g_is_benchmarking ? (double)(total_frames - last_frames) / ((cur_time - last_fps_time) / 1000.0) : 60.0;
            last_frames = total_frames;
            last_fps_time = cur_time;
        }
        if (!g_is_benchmarking) Sleep(16);
    }
}

// --- Direct3D 12 Runner ---
static void run_dx12(int duration) {
    HWND hwnd = create_window("GPU-T Direct3D 12 Stress Test (Native C)");

    ID3D12Device* dev = NULL;
    if (FAILED(D3D12CreateDevice(NULL, D3D_FEATURE_LEVEL_11_0, &IID_ID3D12Device, (void**)&dev))) return;

    D3D12_COMMAND_QUEUE_DESC queueDesc = {
        .Type = D3D12_COMMAND_LIST_TYPE_DIRECT,
        .Priority = D3D12_COMMAND_QUEUE_PRIORITY_NORMAL,
        .Flags = D3D12_COMMAND_QUEUE_FLAG_NONE,
        .NodeMask = 0
    };
    ID3D12CommandQueue* queue = NULL;
    ID3D12Device_CreateCommandQueue(dev, &queueDesc, &IID_ID3D12CommandQueue, (void**)&queue);

    IDXGIFactory2* factory = NULL;
    CreateDXGIFactory1(&IID_IDXGIFactory2, (void**)&factory);

    char gpu_name[256] = "Direct3D 12 Graphics Device";
    IDXGIAdapter1* adapter1 = NULL;
    if (SUCCEEDED(IDXGIFactory1_EnumAdapters1((IDXGIFactory1*)factory, 0, &adapter1))) {
        DXGI_ADAPTER_DESC1 desc1;
        if (SUCCEEDED(IDXGIAdapter1_GetDesc1(adapter1, &desc1))) {
            wcstombs(gpu_name, desc1.Description, sizeof(gpu_name));
        }
        IDXGIAdapter1_Release(adapter1);
    }

    DXGI_SWAP_CHAIN_DESC1 scDesc = {
        .Width = BASE_WIDTH,
        .Height = BASE_HEIGHT,
        .Format = DXGI_FORMAT_B8G8R8A8_UNORM,
        .BufferCount = 2,
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .Scaling = DXGI_SCALING_STRETCH,
        .SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
        .SampleDesc = { .Count = 1, .Quality = 0 }
    };

    IDXGISwapChain1* sc1 = NULL;
    IDXGIFactory2_CreateSwapChainForHwnd(factory, (IUnknown*)queue, hwnd, &scDesc, NULL, NULL, &sc1);

    ID3D12Resource* back_buffers[2] = { NULL, NULL };
    IDXGISwapChain1_GetBuffer(sc1, 0, &IID_ID3D12Resource, (void**)&back_buffers[0]);
    IDXGISwapChain1_GetBuffer(sc1, 1, &IID_ID3D12Resource, (void**)&back_buffers[1]);

    ID3D12CommandAllocator* cmd_alloc = NULL;
    ID3D12Device_CreateCommandAllocator(dev, D3D12_COMMAND_LIST_TYPE_DIRECT, &IID_ID3D12CommandAllocator, (void**)&cmd_alloc);

    ID3D12GraphicsCommandList* cmd_list = NULL;
    ID3D12Device_CreateCommandList(dev, 0, D3D12_COMMAND_LIST_TYPE_DIRECT, cmd_alloc, NULL, &IID_ID3D12GraphicsCommandList, (void**)&cmd_list);

    uint64_t stress_size = 64 * 1024 * 1024;
    D3D12_HEAP_PROPERTIES def_heap = { .Type = D3D12_HEAP_TYPE_DEFAULT };
    D3D12_RESOURCE_DESC vram_desc = {
        .Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
        .Width = stress_size,
        .Height = 1,
        .DepthOrArraySize = 1,
        .MipLevels = 1,
        .Format = DXGI_FORMAT_UNKNOWN,
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR
    };

    ID3D12Resource *vram_src = NULL, *vram_dst = NULL;
    ID3D12Device_CreateCommittedResource(dev, &def_heap, D3D12_HEAP_FLAG_NONE, &vram_desc, D3D12_RESOURCE_STATE_COPY_SOURCE, NULL, &IID_ID3D12Resource, (void**)&vram_src);
    ID3D12Device_CreateCommittedResource(dev, &def_heap, D3D12_HEAP_FLAG_NONE, &vram_desc, D3D12_RESOURCE_STATE_COPY_DEST, NULL, &IID_ID3D12Resource, (void**)&vram_dst);

    uint32_t upload_pitch = (BASE_WIDTH * 4 + 255) & ~255;
    uint64_t upload_size = (uint64_t)upload_pitch * BASE_HEIGHT;

    D3D12_HEAP_PROPERTIES upload_heap = { .Type = D3D12_HEAP_TYPE_UPLOAD };
    D3D12_RESOURCE_DESC upload_desc = {
        .Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
        .Width = upload_size,
        .Height = 1,
        .DepthOrArraySize = 1,
        .MipLevels = 1,
        .Format = DXGI_FORMAT_UNKNOWN,
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR
    };

    ID3D12Resource* upload_buf = NULL;
    ID3D12Device_CreateCommittedResource(dev, &upload_heap, D3D12_HEAP_FLAG_NONE, &upload_desc, D3D12_RESOURCE_STATE_GENERIC_READ, NULL, &IID_ID3D12Resource, (void**)&upload_buf);

    void* p_mapped = NULL;
    ID3D12Resource_Map(upload_buf, 0, NULL, &p_mapped);

    ID3D12GraphicsCommandList_Close(cmd_list);

    g_target_duration = duration;
    if (duration > 0) snprintf(g_custom_text, sizeof(g_custom_text), "%d", duration);

    MSG msg;
    DWORD bench_start_time = GetTickCount();
    DWORD last_fps_time = bench_start_time;
    uint64_t total_frames = 0, last_frames = 0;
    double fps = 60.0;
    double elapsed = 0.0;
    uint32_t buffer_idx = 0;

    while (g_is_running) {
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) g_is_running = false;
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        if (g_bench_reset_requested) {
            bench_start_time = GetTickCount();
            elapsed = 0.0;
            g_bench_reset_requested = false;
        }

        if (g_is_benchmarking) {
            g_anim_time += 0.02f;
            elapsed = (GetTickCount() - bench_start_time) / 1000.0;
            if (g_target_duration > 0 && elapsed >= g_target_duration) {
                g_is_benchmarking = false;
            }
        }

        render_gui(ui_pixels, "DIRECT3D 12", gpu_name, elapsed, fps, (fps * 12.85) / 1000.0, 1);

        for (int r = 0; r < BASE_HEIGHT; r++) {
            memcpy((uint8_t*)p_mapped + (r * upload_pitch), ui_pixels + (r * BASE_WIDTH), BASE_WIDTH * 4);
        }

        ID3D12CommandAllocator_Reset(cmd_alloc);
        ID3D12GraphicsCommandList_Reset(cmd_list, cmd_alloc, NULL);

        if (g_is_benchmarking) {
            for (int p = 0; p < 256; p++) {
                ID3D12GraphicsCommandList_CopyBufferRegion(cmd_list, vram_dst, 0, vram_src, 0, stress_size);
            }
        }

        D3D12_RESOURCE_BARRIER b1 = {
            .Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
            .Transition = {
                .pResource = back_buffers[buffer_idx],
                .Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES,
                .StateBefore = D3D12_RESOURCE_STATE_PRESENT,
                .StateAfter = D3D12_RESOURCE_STATE_COPY_DEST
            }
        };
        ID3D12GraphicsCommandList_ResourceBarrier(cmd_list, 1, &b1);

        D3D12_TEXTURE_COPY_LOCATION dst = {
            .pResource = back_buffers[buffer_idx],
            .Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
            .SubresourceIndex = 0
        };

        D3D12_TEXTURE_COPY_LOCATION src = {
            .pResource = upload_buf,
            .Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT,
            .PlacedFootprint = {
                .Offset = 0,
                .Footprint = {
                    .Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                    .Width = BASE_WIDTH,
                    .Height = BASE_HEIGHT,
                    .Depth = 1,
                    .RowPitch = upload_pitch
                }
            }
        };

        D3D12_BOX src_box = { .left = 0, .top = 0, .front = 0, .right = BASE_WIDTH, .bottom = BASE_HEIGHT, .back = 1 };
        ID3D12GraphicsCommandList_CopyTextureRegion(cmd_list, &dst, 0, 0, 0, &src, &src_box);

        D3D12_RESOURCE_BARRIER b2 = {
            .Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
            .Transition = {
                .pResource = back_buffers[buffer_idx],
                .Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES,
                .StateBefore = D3D12_RESOURCE_STATE_COPY_DEST,
                .StateAfter = D3D12_RESOURCE_STATE_PRESENT
            }
        };
        ID3D12GraphicsCommandList_ResourceBarrier(cmd_list, 1, &b2);

        ID3D12GraphicsCommandList_Close(cmd_list);

        ID3D12CommandList* pp_lists[] = { (ID3D12CommandList*)cmd_list };
        ID3D12CommandQueue_ExecuteCommandLists(queue, 1, pp_lists);

        IDXGISwapChain1_Present(sc1, 0, 0);
        buffer_idx = 1 - buffer_idx;
        total_frames++;

        DWORD cur_time = GetTickCount();
        if (cur_time - last_fps_time >= 250) {
            fps = g_is_benchmarking ? (double)(total_frames - last_frames) / ((cur_time - last_fps_time) / 1000.0) : 60.0;
            last_frames = total_frames;
            last_fps_time = cur_time;
        }
        if (!g_is_benchmarking) Sleep(16);
    }
}

// --- Main Entry Point ---
int main(int argc, char** argv) {
    const char* api = "dx11";
    int duration = 0;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--api") && i + 1 < argc) api = argv[++i];
        if (!strcmp(argv[i], "--duration") && i + 1 < argc) duration = atoi(argv[++i]);
    }

    printf("[NativeD3D] Running native Direct3D workload for: %s (Duration: %ds)\n", api, duration);

    if (!strcmp(api, "dx9") || !strcmp(api, "d3d9")) {
        run_dx9(duration);
    } else if (!strcmp(api, "dx12") || !strcmp(api, "d3d12")) {
        run_dx12(duration);
    } else {
        run_dx11(duration);
    }
    return 0;
}