# CASE-01 同一块 GPU，Metal 与 MoltenVK 报告的能力不同

- 平台：Apple M5 Max · macOS 26.4.1 · Metal vs Vulkan（MoltenVK 1.4.2）
- 发现方式：`NativePlugins/GpuCaps/tests/probe_metal` 与 `probe_vulkan` 输出对比（2026-11-07）

| 字段 | Metal | Vulkan (MoltenVK) |
| --- | --- | --- |
| dedicatedMemoryMB | 38338（recommendedMaxWorkingSetSize） | 49152（DEVICE_LOCAL heap 总和） |
| raytracing | supported（Apple9+ 硬件） | unsupported（未暴露 VK_KHR_ray_tracing_pipeline） |
| vrs | supported（Rasterization Rate Map） | unsupported |
| meshShader | supported（Metal 3） | unsupported（无 VK_EXT_mesh_shader） |
| shaderModel | Metal 4 / Apple10 | Vulkan 1.3 / SPIR-V 1.6 |

- 根因：MoltenVK 是把 Vulkan 翻译到 Metal 的转换层，只暴露它已经实现映射的扩展；“显存”在两套 API 里的定义也不同（工作集预算 vs 堆大小）。
- 结论：能力必须按“设备 × API”记录，不能按设备记录。显存阈值（选档用的 < 4 GB）要注明取自哪个 API。
- 处理：无需回退规则（Unity 在 macOS 上不走 Vulkan），作为能力矩阵的教学样例保留。
