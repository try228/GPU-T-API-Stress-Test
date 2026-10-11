# GPU-T Stress Test & Benchmark Sidecar

Standalone Native AOT GPU benchmark and compute/graphics stress test for **GPU-T**.

---

## Prerequisites

### 1. Build Tools & Runtime
* **.NET 10.0 SDK** (or later)
* **Native AOT toolchain**:
  * **Ubuntu / Debian**: `sudo apt install -y build-essential clang zlib1g-dev libgl1-mesa-dev libvulkan-dev glslang-tools`
  * **Arch Linux**: `sudo pacman -S base-devel clang zlib mesa vulkan-icd-loader glslang`
  * **Fedora**: `sudo dnf install -y clang gcc zlib-devel mesa-libGL-devel vulkan-loader-devel glslang`
* **Shader Compilers**:
  * **Slang Compiler (`slangc`)**: Must be installed and available in `$PATH`. Used by the runtime to compile compute, memory, and modern Khronos/NV2 cooperative matrix shaders into SPIR-V.
  * **GLSL Validator (`glslangValidator`)**: Must be installed and available in `$PATH`. Used by the runtime to compile legacy NVIDIA Cooperative Matrix 1 (`GL_NV_cooperative_matrix`) shaders into native SPIR-V 1.0.

### 2. Runtime Drivers & Optional Tools
* **Vulkan 1.0+** capable driver and loader (`vulkan-tools`, Mesa / NVIDIA / Intel compute drivers). Requires instance extension `VK_KHR_get_physical_device_properties2`.
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
| `-m, --mock-gpu <target>`       | Override hardware detection profile: `nvidia` (RTX 5090), `intel` (Arc B580), `rdna3` (RX 7900 GRE), `pascal`. |
| `--debug`, `-dbg`, `--vk-debug` | Enable Vulkan debug messenger (`VK_EXT_debug_utils`) with colorized console output.                  |
| `--list-gpus`                   | List detected GPUs and PCI IDs, then exit.                                                           |
| `-h, --help`                    | Show help and exit.                                                                                  |

> **Tip:** You can also select the backend using the `API_backend` environment variable:
>
> ```bash
> API_backend=vk ./GPU-T.StressTest
> ```
>
> CLI flags override environment variables.

---

## Supported Backends

### Native Linux

| Identifier           | Target API                                            |
| -------------------- | ----------------------------------------------------- |
| `gl`, `opengl`       | Desktop OpenGL 3.3 Core (**Default**)                 |
| `gles`, `opengles`   | OpenGL ES 3.0                                         |
| `vk`, `vulkan`       | Vulkan 1.0 Compute & Matrix Stress Pipeline           |
| `zink`               | OpenGL over Vulkan (Mesa Zink)                        |
| `zink_es`            | OpenGL ES over Vulkan (Mesa Zink)                     |
| `cl`, `opencl`       | OpenCL 1.2+ Compute                                   |
| `mesa_cl`, `rusticl` | Mesa Rusticl (Rust-based OpenCL)                      |
| `cuda`               | NVIDIA CUDA Driver API (native or via ZLUDA on AMD/Intel) |
| `rocm`, `hip`        | AMD ROCm / HIP Runtime                                |
| `oapi`, `oneapi`     | Intel oneAPI Level Zero                               |

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

### Vulkan Stress Architecture

The Vulkan backend (`vk`) runs a software-rasterized telemetry HUD and multiple compute stress kernels:

* **Device Scoring & Safety Filter:**
  * Uses a priority scoring engine preferring dedicated **Discrete GPUs** over **Integrated GPUs**.
  * Strictly rejects software emulation layers (`VK_PHYSICAL_DEVICE_TYPE_CPU`, Lavapipe, LLVMpipe, SwiftShader) and paravirtualized guest adapters (`VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU`, Virgl, Venus) to avoid driver segmentation faults and IPC virtualization latency skew.
* **Multiplexed Dispatch:** Active workloads run via round-robin submission bursts. Compute kernels do not overlap directly with memory tests, preventing ALU load from skewing cache or bus throughput numbers.
* **ALU Workloads:**
  * **FP32:** Vectorized FMA kernel (4096 FLOPs/thread).
  * **FP16 / BF16:** Packed 16-bit math (8192 / 4096 FLOPs/thread).
  * **FP64:** Double-precision FMA (1024 FLOPs/thread).
  * **INT32 / INT16 / INT8 / INT64:** Native integer arithmetic pipelines.
  * **DP4A:** Native `OpSDotKHR` instruction (`v_dot4_i32_iu8` on RDNA 3 / IDP4A on NVIDIA). Hardened with dynamic input dependencies to prevent NIR/ACO constant-folding.
  * **DP2A:** Native `OpSDotKHR` 16-bit vector dot product. Dynamically queried via `integerDotProduct16BitSignedAccelerated`.
* **Matrix / Tensor Pipelines:**
  * **Dynamic Pipeline Resolution:** Matrix workloads dynamically bind KHR, NV1, or NV2 pipeline variants at dispatch time based on physical hardware support and active UI modifiers.
  * **Standard KHR (`VK_KHR_cooperative_matrix`):** Generic subgroup-scoped matrix engine supporting AMD WMMA, Intel XMX, and NVIDIA Tensor Cores.
  * **NVIDIA NV1 (`VK_NV_cooperative_matrix`):** Legacy subgroup-scoped path for older NVIDIA drivers (2018–2022). Implemented in native GLSL (`GL_NV_cooperative_matrix`) via `glslangValidator` supporting all 7 native types: FP16, FP32, FP64, INT8, INT16, INT32, and INT64. Enabling NV1 in the UI automatically locks NV2 and incompatible formats (BF16, FP8).
  * **NVIDIA NV2 (`VK_NV_cooperative_matrix2`):** Modern workgroup-scoped tensor math path coordinating threads across shared tensor memory. Compiled via Slang with `MemoryScope.Workgroup`.
  * **Intel XMX INT4:** Hardware-accelerated 4-bit integer matrix math via `SPV_INTEL_int4` on Intel Arc / Battlemage silicon.
  * Supported types: FP16, BF16, FP8 (E4M3 / E5M2), FP32, FP64, INT8, INT16, INT32, INT64, INT4.
* **Memory & Cache Hierarchy:**
  * **Adaptive VRAM Bus Streaming:** Dynamically sizes memory sweep buffers between 256 MB and 1024 MB (`TotalVRAM / 4`) based on probed on-die cache capacity. Guarantees complete exhaustion of AMD Infinity Cache (up to 128 MB) and NVIDIA L2 (up to 96 MB), measuring true physical GDDR/HBM bus saturation rather than on-chip SRAM cache hits.
  * **L1/L2 Cache:** Operates on a safe 1 MB footprint, ensuring working sets fit strictly inside L2 across all architectures (including 2 MB L2 on Pascal GTX 1080) without spilling to VRAM.
  * **L3 Infinity Cache:** Probed via silicon topology rules (8–128 MB). Automatically disabled (`N/A`) on GPUs lacking hardware L3.
  * **Integrated GPUs (UMA):** Automatically disables dedicated L1/L2 and L3 cache benchmarks (`N/A`) if the probe database reports zero dedicated GPU cache, falling back to measuring shared system RAM throughput and interconnect latency.
  * **Latency:** Serial pointer-chasing kernel to test interconnect round-trip latency.

### Vulkan Validation

Validation layers are not linked into the binary. To debug pipeline issues, run with the loader layer enabled:

```bash
VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation ./GPU-T.StressTest -b vk --debug
```

### Mock Profiles

Use `--mock-gpu` to test device probing and UI layout without specific physical hardware:

```bash
./GPU-T.StressTest -b vk -m nvidia   # RTX 5090 Blackwell profile (128 MB L2)
./GPU-T.StressTest -b vk -m rdna3    # RX 7900 GRE RDNA 3 profile (Infinity Cache)
./GPU-T.StressTest -b vk -m intel    # Arc B580 Battlemage profile (Intel XMX / Int4)
./GPU-T.StressTest -b vk -m pascal   # GTX 1080 Legacy Pascal profile (2 MB L2, No L3, No Tensor)
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