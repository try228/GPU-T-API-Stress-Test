# GPU-T Stress Test & Benchmark Sidecar

Standalone **Native AOT GPU API benchmark and compute/graphics stress-test sidecar** for **GPU-T**.

## Build

### 1. Publish the Windows Direct3D payload

Publishes a compressed and trimmed `win-x64` Direct3D payload:

```bash
dotnet publish Payloads/D3DPayload \
  -c Release \
  -r win-x64 \
  -o bin/Release/net10.0/linux-x64/publish/payloads
```

### 2. Optional: Build the experimental native C Direct3D payload

Requires `mingw-w64-gcc`:

```bash
x86_64-w64-mingw32-gcc -O3 -s -mwindows \
  Payloads/D3DPayload/NativeD3D/d3dstress.c \
  -o bin/Release/net10.0/linux-x64/publish/payloads/d3d_stress_native.exe \
  -ld3d9 -ld3d11 -ld3d12 -ldxgi -ldxguid -lm
```

The native payload can be selected at runtime with `--native-d3d`.

### 3. Publish the Linux Native AOT sidecar

```bash
dotnet publish -c Release -r linux-x64
```

---

## Usage

```bash
./bin/Release/net10.0/linux-x64/publish/GPU-T.StressTest [options]
```

### Options

| Option                          | Description                                                                                                               |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `-b, --backend <api>`           | Target backend API. Default: `gl`.                                                                                        |
| `-g, --gpu <index\|name>`       | Target GPU by numeric index, such as `0` or `1`, or by a name/vendor substring, such as `amd`, `intel`, or `nvidia`.      |
| `-d, --duration <sec>`          | Initial stress-test duration in seconds. Use `0` for unlimited duration.                                                  |
| `-m, --mock-gpu <target>`       | Simulate a virtual GPU architecture profile: `nvidia` (RTX 5090), `intel` (Arc B580), `rdna3` (RX 7900 GRE), `pascal`.    |
| `-n, --native-d3d`              | Use the experimental lightweight native C Direct3D payload (`d3d_stress_native.exe`) instead of the managed .NET payload. |
| `--debug`, `-dbg`, `--vk-debug` | Enable Vulkan debug messenger (`VK_EXT_debug_utils`) colored callback logging in console.                                 |
| `--list-gpus`                   | Enumerate all detected GPU devices, including PCI IDs, and exit.                                                          |
| `-h, --help`                    | Display CLI usage information.                                                                                            |

> **Note:** The backend can also be specified through the `API_backend` environment variable:
>
> ```bash
> API_backend=vk ./GPU-T.StressTest
> ```
>
> Command-line arguments take precedence over environment variables.

---

## Native Linux Backends

| Identifier           | Target Graphics / Compute API                         |
| -------------------- | ----------------------------------------------------- |
| `gl`, `opengl`       | Desktop OpenGL 3.3 Core — **Default**                 |
| `gles`, `opengles`   | OpenGL ES 3.0                                         |
| `vk`, `vulkan`       | Vulkan 1.0 Baseline Compute & Silicon Stress Matrix   |
| `zink`               | Zink — OpenGL over Vulkan                             |
| `zink_es`            | Zink — OpenGL ES over Vulkan                          |
| `cl`, `opencl`       | OpenCL 1.2+ compute                                   |
| `mesa_cl`, `rusticl` | Mesa Rusticl — Rust-based OpenCL                      |
| `cuda`               | NVIDIA CUDA Driver API — native or ZLUDA on AMD/Intel |
| `rocm`, `hip`        | AMD ROCm / HIP Runtime                                |
| `oapi`, `oneapi`     | Intel oneAPI Level Zero                               |

---

## Windows Direct3D Backends via Wine / Proton

| Identifier                   | Direct3D Version & Translation Layer            |
| ---------------------------- | ----------------------------------------------- |
| `dxvk_9`, `dx9`              | Direct3D 9 over Vulkan via DXVK                 |
| `wd3d_9`                     | Direct3D 9 over OpenGL via WineD3D              |
| `dxvk_11`, `dx11`, `dxvk`    | Direct3D 11 over Vulkan via DXVK                |
| `wd3d_11`, `wd3d`, `wined3d` | Direct3D 11 over OpenGL via WineD3D             |
| `vkd3d`                      | Direct3D 12 over Vulkan via Wine upstream VKD3D |
| `vkd3d_p`, `dx12`            | Direct3D 12 over Vulkan via Valve VKD3D-Proton  |

---

## Notes & Architecture

### Vulkan Stress Matrix & Silicon Telemetry

The Vulkan backend (`vk`) features a comprehensive software rendering HUD and a customizable workload configuration matrix:

* **Isolated Round-Robin Dispatch**: Multiple active workloads are executed using per-submit time multiplexing. This ensures heavy compute shaders (e.g. FP32) do not dilute memory latency or cache bandwidth measurements, presenting pure unpolluted hardware metrics simultaneously.
* **Hardware-Accurate ALU Engines:**

  * **FP32 Core FMA:** Standardized 4096 FLOPs/invocation compute kernel.
  * **FP16/BF16 Packed Math:** 4096 FLOPs/invocation half-precision math.
  * **FP64 Double Prec:** 2048 FLOPs/invocation 64-bit precision math.
  * **INT32 / INT64 / INT16 / INT8:** Discrete integer ALU pipelines.
  * **INT8 DP4A & INT16 DP2A:** Hardware dot product instructions (`OpSDotKHR`), executing natively on NVIDIA Turing/Ampere/Ada (`IDP2A`) and AMD RDNA (`V_DOT4` / `V_PK_MAD_I16` packed math).
* **Adaptive Cache & Memory Probing:**

  * **L1/L2 Cache Bandwidth:** Dynamically scales workgroup grids to fit 75% of the probed hardware L2 cache, measuring bidirectional cache throughput (TB/s) without flushing to VRAM.
  * **L3 Infinity Cache:** Specifically probed for AMD discrete RDNA 2/3/4 (8 MB to 128 MB) and RDNA 3.5 Halo APUs (8040S, 8050S, 8060S, 8065S). Strictly disabled for GPUs without L3.
  * **VRAM Stream Bandwidth:** Measures physical bus dataset throughput (GB/s) over a 128 MB allocation.
  * **Memory Latency:** 1024-step serial pointer-chasing kernel measuring physical fabric transit latency (ns).

### Vulkan Debugging & Validation

Validation layers are decoupled from application binaries. To enable validation layer diagnostic interception, use standard Vulkan Loader environment variables combined with the `--debug` CLI flag:

```bash
# Run with Khronos validation layer and colorized console logging:
VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation ./GPU-T.StressTest -b vk --debug
```

When `--debug` (or `-dbg` / `--vk-debug`) is passed, the engine registers `VK_EXT_debug_utils` and prints color-coded warnings and error notifications directly to `stdout`.

### Mock GPU Profiles

To simulate hardware feature combinations (such as AMD Infinity Cache or NVIDIA Blackwell 128 MB L2) without physical hardware access, use `--mock-gpu`:

```bash
./GPU-T.StressTest -b vk -m nvidia   # Simulates RTX 5090 Blackwell profile
./GPU-T.StressTest -b vk -m rdna3    # Simulates RX 7900 GRE RDNA 3 profile
./GPU-T.StressTest -b vk -m intel    # Simulates Arc B580 Battlemage profile
```

### Device Selection

Compute APIs such as **Vulkan, OpenCL, CUDA, ROCm, and oneAPI**, as well as Vulkan-based Direct3D translation layers such as **DXVK** and **VKD3D/VKD3D-Proton**, can directly target the GPU selected with:

```bash
-g <index|name>
```

### OpenGL and WineD3D Multi-GPU Behavior

Native OpenGL backends (`gl`, `gles`) render through the display server's default screen context and therefore cannot reliably select a secondary GPU using `-g`.

**WineD3D has the same limitation**, since its rendering path also goes through OpenGL. As a result, the `wd3d_9` and `wd3d_11` backends use the GPU selected by the underlying OpenGL/display context rather than directly honoring GPU selection in the same way as Vulkan-based backends.

For native OpenGL, a secondary GPU can instead be targeted through Zink:

```bash
./GPU-T.StressTest -b zink -g <index>
```

There is intentionally **no equivalent WineD3D-over-Zink path**. Stacking multiple translation layers, for example:

```text
WineD3D → OpenGL → Zink → Vulkan
```

would add unnecessary complexity and overhead and is not considered a practical use case.

### Direct3D Payload Selection

By default, Windows Direct3D backends use the managed .NET Direct3D payload.

To use the experimental lightweight native C implementation instead, build `d3d_stress_native.exe` and run:

```bash
./GPU-T.StressTest -b dxvk_11 --native-d3d
```

The `--native-d3d` option applies to the Windows Direct3D payload path and does not affect native Linux graphics or compute backends.

### ZLUDA Support

The CUDA backend dynamically detects whether it is running with native NVIDIA drivers or through **ZLUDA** translation on supported AMD or Intel hardware.

### Rusticl Initialization

The Mesa Rusticl backend may experience a brief initialization delay while kernels are JIT-compiled through Clang/LLVM.
