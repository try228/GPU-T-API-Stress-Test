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

| Option                    | Description                                                                                                               |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `-b, --backend <api>`     | Target backend API. Default: `gl`.                                                                                        |
| `-g, --gpu <index\|name>` | Target GPU by numeric index, such as `0` or `1`, or by a name/vendor substring, such as `amd`, `intel`, or `nvidia`.      |
| `-d, --duration <sec>`    | Initial stress-test duration in seconds. Use `0` for unlimited duration.                                                  |
| `-n, --native-d3d`        | Use the experimental lightweight native C Direct3D payload (`d3d_stress_native.exe`) instead of the managed .NET payload. |
| `--list-gpus`             | Enumerate all detected GPU devices, including PCI IDs, and exit.                                                          |
| `-h, --help`              | Display CLI usage information.                                                                                            |

> **Note:** The backend can also be specified through the `API_backend` environment variable:
>
> ```bash
> API_backend=cl ./GPU-T.StressTest
> ```
>
> Command-line arguments take precedence over environment variables.

---

## Native Linux Backends

| Identifier           | Target Graphics / Compute API                         |
| -------------------- | ----------------------------------------------------- |
| `gl`, `opengl`       | Desktop OpenGL 3.3 Core — **Default**                 |
| `gles`, `opengles`   | OpenGL ES 3.0                                         |
| `vk`, `vulkan`       | Vulkan 1.0 baseline compute queue                     |
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
