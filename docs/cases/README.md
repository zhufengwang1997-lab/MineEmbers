# 平台差异案例库

| 编号 | 一句话 | 类别 |
| --- | --- | --- |
| [CASE-01](CASE-01-moltenvk-vs-metal.md) | 同一块 M5 Max，Metal 与 MoltenVK 报告的光追 / VRS / Mesh Shader / 显存不同 | 能力探测 |
| [CASE-02](CASE-02-engine-vs-hardware-raytracing.md) | 硬件支持光追，`SystemInfo.supportsRayTracing` 却为 false | 能力分层 |
| [CASE-03](CASE-03-first-build-crash-postprocessor.md) | 生成资源后首次构建崩在包内 AssetPostprocessor，重跑成功 | 工具链 |
| [CASE-04](CASE-04-realtime-reflection-probes-off.md) | 质量级别关闭实时反射探针，探针被静默跳过 | 工程配置 |
| [CASE-05](CASE-05-point-shadow-atlas-overflow.md) | 点光源阴影图集溢出，High 反而比 Medium 糊 | 档位设计 |
| [CASE-06](CASE-06-gsc-trace-release-build.md) | GSC 在 Release 包里"录制成功"却是空的 | 新 API 验收 |
| [CASE-07](CASE-07-frame-timing-double-count.md) | 帧时间延迟到达被记两次，启动帧冒充 1 秒卡顿 | 测量工具 |
| [CASE-08](CASE-08-lake-hitch-and-runner-garbage.md) | 地下湖偶发卡顿的排除法；顺带把每次运行的 GC 从 38 次降到 5 次 | 测量工具 / CPU |

每个案例一个文件：`CASE-NN-<短名>.md`，模板如下。`compat_overrides.json` 的每条规则都必须引用一个案例编号。

```markdown
# CASE-NN 标题
- 平台：API / 设备 / 驱动
- 现象：（截图或抓帧链接，放 ../captures/）
- 复现：步骤 + 机位编号
- 定位：用了什么工具，看到了什么
- 根因：
- 修复 / 回退：代码改动或 compat 规则
- 验证：screenshot_diff / compare 结果
```
