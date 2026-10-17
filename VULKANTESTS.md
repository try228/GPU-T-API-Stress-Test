### Vulkan Stress Architecture

The Vulkan backend (`vk`) runs a software-rasterized telemetry HUD and multiple compute, ray tracing, graphics, and video stress kernels:

* **Device Scoring & Safety Filter:**
  * Uses a priority scoring engine preferring dedicated **Discrete GPUs** over **Integrated GPUs**.
  * Strictly rejects software emulation layers (`VK_PHYSICAL_DEVICE_TYPE_CPU`, Lavapipe, LLVMpipe, SwiftShader) and paravirtualized guest adapters (`VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU`, Virgl, Venus) to avoid driver segmentation faults and IPC virtualization latency skew.
* **Multiplexed Dispatch:** Active workloads run via round-robin submission bursts. Compute kernels do not overlap directly with memory tests, preventing ALU load from skewing cache or bus throughput numbers.
* **Hardware Ray Tracing (BVH & Silicon Accelerators):**
  * **On-GPU Acceleration Structure Builder:** Dynamically allocates device-addressable memory (`ShaderDeviceAddressBit`) and constructs real two-level acceleration structures (multi-triangle BLAS + instanced TLAS) using `VK_KHR_acceleration_structure` and `VK_KHR_buffer_device_address`.
  * **True Silicon Execution:** Targets SPIR-V 1.4 instructions (`OpRayQueryInitializeKHR`, `OpRayQueryProceedKHR`) binding directly to `VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR` (Binding 7). Triggers physical silicon traversal units (AMD RDNA 2/3 Ray Accelerators via `IMAGE_BVH64`, NVIDIA RT Cores, Intel Xe-HPG/Xe2 RTUs) rather than software ALU fallbacks.
  * **Microbenchmark Peak Throughput:** Operates on an in-cache BVH footprint (100% L0/L1 cache hit rate, zero VRAM latency stalls) to measure the raw **Peak Silicon Traversal Rate**.
  * **Workloads & Modifiers:**
    * **Ray Query (Inline):** Direct inline compute BVH traversal measuring raw hardware intersection rates.
    * **RT Pipeline (HW):** Simulates multi-stage ray dispatch (primary ray intersection + secondary shadow rays).
    * **SER Reordering (`rt_ser`):** Tests Shader Execution Reordering (`VK_KHR_ray_tracing_invocation_reorder` / `VK_NV_ray_tracing_invocation_reorder`) with spatial Morton-code binning across wavefronts/warps. *Strictly requires RT Pipeline.*
    * **OMM Opacity Micromaps (`rt_omm`):** Evaluates 2-state micro-triangle opacity bitmasks directly during traversal (`VK_EXT_opacity_micromap`). *Compatible with Ray Query and RT Pipeline.*
    * **RT Position Fetch (`rt_pos`):** Tests hardware position fetch (`VK_KHR_ray_tracing_position_fetch`) vertex reconstruction and secondary bounce reflections. *Compatible with Ray Query and RT Pipeline.*
    * **Legacy NV_RT (`rt_nv`):** Turing/Pascal compute path for 2018 emulation drivers. *Strictly mutually exclusive with modern KHR ray tracing.*
* **Hardware Video Engines (VCN / NVDEC / NVENC / QuickSync):**
  * **Vulkan Video Queues (`VK_KHR_video_queue`):** Discovers and binds dedicated hardware video decode and encode queue families (`VK_QUEUE_VIDEO_DECODE_BIT_KHR`, `VK_QUEUE_VIDEO_ENCODE_BIT_KHR`), completely independent of compute/graphics queues.
  * **Native Codec Session Allocation:** Dynamically queries capabilities and constructs physical codec sessions (`VkVideoSessionKHR`) using `vkCreateVideoSessionKHR`, queries memory requirements, and binds dedicated device-local device memory via `vkBindVideoSessionMemoryKHR` for H.264, H.265/HEVC, AV1, and VP9 profiles.
  * **Dynamic Linux DRM Render Node Matching:** Resolves the exact `/dev/dri/renderD*` node by querying Linux sysfs (`/sys/class/drm/renderD*/device/`) and matching the Vulkan physical device's `VendorID` and `DeviceID`. This guarantees hardware operations target the selected discrete GPU (e.g. AMD Radeon RX 7900 GRE on `renderD129`) rather than accidental fallback to CPU-integrated graphics (e.g. Intel UHD 750 or AMD Raphael iGPU on `renderD128`).
  * **Double-Buffered Vulkan Synchronization:** Uses an in-flight fence architecture (`InFlightFrames = 2`) with double-buffered command buffers for video submissions, preventing orphaned fence deadlocks when toggling or stopping benchmarks.
  * **Hybrid ASIC Saturation Burner:** Runs a non-blocking asynchronous hardware burner via the DRM render node with zero CPU overhead (`nullsrc` $\rightarrow$ `hwupload` $\rightarrow$ VA-API). Drives physical video microcontrollers (AMD VCN, NVIDIA NVDEC, Intel QuickSync) to 80–100% duty cycle in telemetry tools (`amdgpu_top`, `intel_gpu_top`). Features automatic fallback cascades (e.g. falling back to HEVC/H.264 if a specific GPU generation lacks hardware AV1 or VP9 encode entrypoints).
  * **Workloads:**
    * **Decode:** AV1 (`vid_dec_av1`), HEVC (`vid_dec_hevc`), H.264 (`vid_dec_h264`), VP9 (`vid_dec_vp9`).
    * **Encode:** AV1 (`vid_enc_av1`), HEVC (`vid_enc_hevc`), H.264 (`vid_enc_h264`).
* **Graphics Pipeline (ROP Fillrate & Depth/Stencil Throughput):**
  * **ROP / Rasterization Fillrate:** Tests pixel blending and raster operation units across standard, HDR, and high-precision render targets (`rop_rgba8`, `rop_rgba16f`, `rop_rgba32f`), Alpha Blending throughput (`rop_blend`), 4x/8x Multi-Sample Anti-Aliasing (`rop_msaa`), and Multiple Render Targets (`rop_mrt2`, `rop_mrt4`, `rop_mrt8`).
  * **Depth & Stencil Throughput:** Stress-tests hardware depth/stencil rejection and write engines: Depth Testing (`ds_test`), Depth Write (`ds_write`), Early-Z fragment rejection (`ds_earlyz`), Hierarchical-Z (`ds_hiz`), and 8-bit Stencil buffer operations (`ds_stencil`).
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