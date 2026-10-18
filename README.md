# GPU-T Stress Test & Benchmark Sidecar

Standalone Native AOT GPU benchmark and compute/graphics stress test for **GPU-T**.

---

## Prerequisites

### 1. Build Tools & Runtime
* **.NET 10.0 SDK** (or later)
* **Native AOT toolchain & system libraries**:
  * **Ubuntu / Debian**: `sudo apt install -y build-essential clang zlib1g-dev libgl1-mesa-dev libvulkan-dev glslang-tools spirv-tools`
  * **Arch Linux**: `sudo pacman -S base-devel clang zlib mesa vulkan-icd-loader glslang spirv-tools`
  * **Fedora**: `sudo dnf install -y clang gcc zlib-devel mesa-libGL-devel vulkan-loader-devel glslang spirv-tools`
* **Shader Compilers & Assemblers**:
  * **Slang Compiler (`slangc`)**: Must be installed and available in `$PATH`. Compiles compute, graphics (ROP/Depth), memory, modern Khronos/NV2 cooperative matrix, and Hardware Ray Tracing shaders into SPIR-V (profiles `spirv_1_0`, `spirv_1_3`, and `spirv_1_4`).
  * **GLSL Validator (`glslangValidator`)**: Must be installed and available in `$PATH`. Compiles legacy NVIDIA Cooperative Matrix 1 (`GL_NV_cooperative_matrix`) and Legacy Ray Tracing (`rt_nv.comp`) compute shaders into native SPIR-V.
  * **SPIR-V Assembler (`spirv-as`)**: Must be installed and available in `$PATH` (provided by `spirv-tools`). Used by the build target to assemble raw SPIR-V 1.3 assembly shaders (`.spvasm`) for specialized matrix/tensor instructions.
  * *(Note: Hardware Video workloads operate directly on fixed-function ASIC silicon and do not require SPIR-V shaders.)*

### 2. Runtime Drivers & Optional Tools
* **Vulkan 1.0+** capable driver and loader (`vulkan-tools`, Mesa / NVIDIA / Intel compute drivers). Requires instance extension `VK_KHR_get_physical_device_properties2`.
* **Hardware Ray Tracing** requires driver support for `VK_KHR_acceleration_structure`, `VK_KHR_buffer_device_address`, and `VK_KHR_ray_query` / `VK_KHR_ray_tracing_pipeline`.
* **Hardware Video Engines** require driver support for `VK_KHR_video_queue`, `VK_KHR_video_decode_queue`, and/or `VK_KHR_video_encode_queue`.
* **FFmpeg (`ffmpeg`)**: Recommended on Linux with VA-API support (`ffmpeg -hwaccel vaapi`) for 100% ASIC power and thermal saturation of hardware media engines (AMD VCN, Intel QuickSync, NVIDIA NVENC/NVDEC).
* **Wine / Proton** (required only for Windows Direct3D backends via DXVK / VKD3D).
* **Vulkan Validation Layers** (`vulkan-validation-layers`), optional, for `--debug`.

---

## Build

### 1. Windows Direct3D payload (Optional)

Publishes a trimmed `win-x64` payload used for Wine/Proton translation benchmarks:

```bash
dotnet publish Payloads/D3DPayload \
  -c Release \
  -r win-x64 \
  -o bin/Release/net10.0/linux-x64/publish/payloads
```

### 2. Linux Native AOT binary

```bash
dotnet publish -c Release -r linux-x64
```

The build pipeline automatically invokes `slangc`, `glslangValidator`, and `spirv-as` to compile all shaders into SPIR-V binaries and embed them directly into the output executable.

The resulting binary will be located at:
`./bin/Release/net10.0/linux-x64/publish/GPU-T.StressTest`

---

## Usage

```bash
./bin/Release/net10.0/linux-x64/publish/GPU-T.StressTest [options]
```

### Options

| Option                          | Description                                                                                          |
| ------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `-b, --backend <api>`           | Target backend API. Default: `gl`.                                                                   |
| `-g, --gpu <index\|name>`       | Target GPU by numeric index (`0`, `1`) or vendor/device substring (`amd`, `intel`, `nvidia`).        |
| `-d, --duration <sec>`          | Test duration in seconds (`0` for unlimited). Default: `0`.                                          |
| `-m, --mock-gpu <target>`       | Override hardware detection profile: `nvidia` (RTX 5090), `intel` (Arc B580), `rdna3` (RX 7900 GRE), `pascal` (GTX 1080). |
| `--experimental`                | Enable experimental unfinished backends.                                                             |
| `--debug`, `-dbg`, `--vk-debug` | Enable Vulkan debug messenger (`VK_EXT_debug_utils`) with colorized console output.                  |
| `--list-gpus`                   | List detected GPUs and PCI IDs, then exit.                                                           |
| `-h, --help`                    | Show help and exit.                                                                                  |

> **Tip:** You can also select the backend using the `API_backend` environment variable:
>
> ```bash
> API_backend=vk ./GPU-T.StressTest
> ```
>
> And select unfinished backends via `GPUT_EXPERIMENTAL=1` environment variable:
> ```bash
> GPUT_EXPERIMENTAL=1 ./GPU-T.StressTest
> ```
>
> CLI flags override environment variables.

---

## Supported Backends

### Native Linux

| Identifier           | Target API                                                               |
| -------------------- | ------------------------------------------------------------------------ |
| `gl`, `opengl`       | Desktop OpenGL 3.3 Core (**Default**)                                    |
| `gles`, `opengles`   | OpenGL ES 3.0                                                            |
| `vk`, `vulkan`       | Vulkan 1.0/1.3 Compute, Matrix, Hardware RT, Graphics & Video Pipelines  |
| `zink`               | OpenGL over Vulkan (Mesa Zink)                                           |
| `zink_es`            | OpenGL ES over Vulkan (Mesa Zink)                                        |
| `cl`, `opencl`       | OpenCL 1.2+ Compute                                                      |
| `mesa_cl`, `rusticl` | Mesa Rusticl (Rust-based OpenCL)                                         |
| `cuda`               | NVIDIA CUDA Driver API (native or via ZLUDA on AMD/Intel)                |
| `rocm`, `hip`        | AMD ROCm / HIP Runtime                                                   |
| `oapi`, `oneapi`     | Intel oneAPI Level Zero                                                  |

### Direct3D via Wine / Proton

| Identifier                   | Translation Layer                                     |
| ---------------------------- | ----------------------------------------------------- |
| `dxvk_9`, `dx9`              | Direct3D 9 over Vulkan (DXVK)                         |
| `wd3d_9`                     | Direct3D 9 over OpenGL (WineD3D)                      |
| `dxvk_11`, `dx11`, `dxvk`    | Direct3D 11 over Vulkan (DXVK)                        |
| `wd3d_11`, `wd3d`, `wined3d` | Direct3D 11 over OpenGL (WineD3D)                     |
| `vkd3d`                      | Direct3D 12 over Vulkan (upstream Wine VKD3D)         |
| `vkd3d_p`, `dx12`            | Direct3D 12 over Vulkan (Valve VKD3D-Proton)          |

---

## Technical Notes

### Vulkan Architecture & Workload Details

Detailed documentation regarding the Vulkan stress test pipelines has been extracted into a dedicated guide:

👉 **[VULKANTESTS.md](VULKANTESTS.md)**

It covers in-depth technical specifications for:
* **Device Scoring & Adapter Filtering:** Automatic discrete GPU prioritization and rejection of virtualized/software drivers.
* **ALU Pipelines:** FP32, FP16, BF16, FP64, INT8–INT64, and vectorized DP4A/DP2A dot products.
* **Cooperative Matrix / Tensor Math:** Subgroup KHR, legacy NVIDIA NV1, workgroup-scoped NV2, and Intel XMX INT4.
* **Hardware Ray Tracing:** In-GPU BVH construction (BLAS/TLAS), Ray Query, RT Pipeline, SER, OMM, and Position Fetch.
* **Memory & Cache Hierarchy:** Adaptive sweep buffer sizing, L1/L2 cache, L3 Infinity Cache, and pointer-chasing latency.
* **Graphics Pipeline:** ROP fillrate (RGBA8, RGBA16F, RGBA32F, Blending, MSAA, MRT2/4/8) and Depth/Stencil throughput (Early-Z, Hi-Z).
* **Hardware Video Engines:** Vulkan Video queue synchronization, Linux DRM node matching (`renderD*`), and hybrid hardware ASIC burning (AV1, HEVC, H.264, VP9).
* **Validation & Debugging:** Step-by-step instructions for enabling `VK_LAYER_KHRONOS_validation`.

### Mock Profiles

Use `--mock-gpu` to test device probing, UI layout, and RT matrix locking without specific physical hardware:

```bash
./GPU-T.StressTest -b vk -m nvidia   # RTX 5090 Blackwell profile (128 MB L2, SER, OMM, PosFetch, Full Tensor)
./GPU-T.StressTest -b vk -m rdna3    # RX 7900 GRE RDNA 3 profile (Infinity Cache, Ray Query, PosFetch, WMMA)
./GPU-T.StressTest -b vk -m intel    # Arc B580 Battlemage profile (Intel XMX / Int4, Hardware RT)
./GPU-T.StressTest -b vk -m pascal   # GTX 1080 Legacy Pascal profile (2 MB L2, No L3, Legacy NV_RT 2018)
```

### Multi-GPU Selection Limitations

* **Vulkan, OpenCL, CUDA, ROCm, Level Zero, DXVK, and VKD3D** can bind directly to any GPU via `-g <index|name>`.
* **Native OpenGL (`gl`, `gles`) and WineD3D (`wd3d_*`)** cannot reliably switch GPUs via `-g` because context creation depends on the default display server screen.
* To target a secondary GPU with OpenGL, use Zink:

  ```bash
  ./GPU-T.StressTest -b zink -g 1
  ```

*(There is no WineD3D-over-Zink path, as chaining WineD3D → OpenGL → Zink → Vulkan adds unnecessary overhead.)*

### ZLUDA & Rusticl

* **CUDA Backend:** Automatically detects if running on native NVIDIA hardware or through ZLUDA on AMD/Intel GPUs.
* **Rusticl:** Expect a brief delay on startup while OpenCL kernels are JIT-compiled by Clang/LLVM.
```