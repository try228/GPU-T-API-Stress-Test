# GPU-T Stress Test & Benchmark Sidecar

A standalone Native AOT GPU API benchmark and compute stress-test sidecar for GPU-T.

## Build

```bash
dotnet publish -c Release -r linux-x64
```

## Usage

```bash
./bin/Release/net10.0/linux-x64/publish/GPU-T.StressTest [options]
```

### Options

| Option                      | Description                                                                                                         |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `-b`, `--backend <api>`     | Target backend API. Default: `vk`.                                                                                  |
| `-g`, `--gpu <index\|name>` | Target GPU by numeric index (for example, `0` or `1`) or name substring (for example, `amd`, `intel`, or `nvidia`). |
| `-d`, `--duration <sec>`    | Initial test duration in seconds. Use `0` for unlimited duration.                                                   |
| `--list-gpus`               | List all detected Vulkan GPU devices and exit.                                                                      |
| `-h`, `--help`              | Show CLI usage and available options.                                                                               |

> **Note:** The backend can also be selected using the `API_backend` environment variable.
>
> ```bash
> API_backend=cl ./GPU-T.StressTest
> ```
>
> Command-line arguments take precedence over the environment variable.

## Supported Backends

| Identifier           | API / Runtime                           |
| -------------------- | --------------------------------------- |
| `vk`, `vulkan`       | Vulkan 1.0 Compute Queue                |
| `gl`, `opengl`       | Desktop OpenGL 3.3 Core                 |
| `gles`, `opengles`   | OpenGL ES 3.0                           |
| `zink`               | Zink — OpenGL over Vulkan               |
| `zink_es`            | Zink — OpenGL ES over Vulkan            |
| `cl`, `opencl`       | OpenCL 1.2+ Compute                     |
| `mesa_cl`, `rusticl` | Mesa Rusticl — Rust-based OpenCL        |
| `cuda`               | NVIDIA CUDA Driver API (Native / ZLUDA) |
| `rocm`, `hip`        | AMD ROCm / HIP Runtime                  |
| `oapi`, `oneapi`     | Intel oneAPI Level Zero                 |

## Work in Progress: Windows Translation Layers

> These backends are still experimental and may be incomplete or unstable.

| Identifier | Translation Layer                                       |
| ---------- | ------------------------------------------------------- |
| `dxvk`     | Direct3D 11 over Vulkan via Wine / Proton               |
| `vkd3d`    | Direct3D 12 over Vulkan via Wine                        |
| `vkd3d_p`  | Direct3D 12 over Vulkan via VKD3D-Proton / Steam Proton |
| `wd3d`     | Direct3D over OpenGL via WineD3D                        |

## Notes and Limitations

### Compute Device Selection

Vulkan, OpenCL, CUDA, ROCm/HIP, and oneAPI dispatch compute workloads directly to the GPU selected with `-g`.

### OpenGL and Multi-GPU Systems

Native OpenGL backends (`gl` and `gles`) use the display server's default screen context.

To target a secondary GPU with an OpenGL workload, use Zink instead:

```bash
./GPU-T.StressTest -b zink -g <index>
```

### Rusticl

Rusticl may introduce a short delay during initialization because kernels are JIT-compiled through Clang/LLVM.
