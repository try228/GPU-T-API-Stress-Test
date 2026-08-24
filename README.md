# GPU-T API/Stress Test

A standalone GPU API benchmark and stress test designed for potential integration into GPU-T.

## Build

Clone the repository and run:

```bash
dotnet publish
```

## Run

Run the application with the `API_backend` environment variable:

```bash
API_backend=api ./bin/Release/net10.0/linux-x64/publish/GpuT.Agent
```

Replace `api` with one of the supported backends:

| Backend   | API                        |
| --------- | -------------------------- |
| `vk`      | Vulkan                     |
| `gl`      | OpenGL                     |
| `gles`    | OpenGL ES                  |
| `zink`    | Zink over OpenGL           |
| `zink_es` | Zink over OpenGL ES        |
| `cl`      | Full OpenCL implementation |
| `mesa_cl` | Mesa Rusticl               |
| `cuda`    | NVIDIA CUDA                |
| `rocm`    | AMD ROCm                   |
| `oapi`    | Intel oneAPI               |

## WIP APIs

The following backends are currently work in progress:

| Backend   | API                        |
| --------- | -------------------------- |
| `dxvk`    | DXVK                       |
| `vkd3d`   | VKD3D                      |
| `vkd3d_p` | VKD3D-Proton               |
| `wd3d`    | WineD3D                    |

## Known Issues

* Using DRI_PRIME with OpenGL/OpenGL ES and related APIs (OpenCL, CUDA, ROCm, oneAPI) is currently not supported.
* Rusticl may take a long time to open or close.

