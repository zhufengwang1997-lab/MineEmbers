# CASE-02 硬件支持光追，Unity 却报告不支持

- 平台：Apple M5 Max · Metal · Unity 6000.3.28f1 · URP 17.3.0
- 发现方式：EditMode 测试 `GpuCapsTests.ReportIsComplete` 的报告（2026-11-07）

| 来源 | 光追 |
| --- | --- |
| GpuCaps 插件（MTLDevice.supportsRaytracing + Apple9 family） | supported（hardware） |
| `SystemInfo.supportsRayTracing` | false |

- 根因：两者回答的是不同的问题。插件问的是“这块 GPU / 驱动能不能做”；`SystemInfo.supportsRayTracing` 问的是“Unity 的 RayTracingShader / RayTracingAccelerationStructure 能不能在当前配置下用”，它依赖渲染管线与平台后端的支持。
- 结论：报告里同时保留两层信息（`features.raytracing` 与 `unity.supportsRayTracing`）。业务代码要用 Unity 的光追 API 时看后者；做平台能力统计、评估“换管线是否值得”时看前者。
- 处理：无需修复；在《多平台抓帧分析流程》中作为“能力分层”的例子。
