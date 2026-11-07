# CASE-04 湖面没有反射：质量级别里关闭了实时反射探针

- 平台：全平台（在 Apple M5 Max · Metal 上发现）· URP 17.3.0
- 现象：地下湖的湖面始终是纯黑，看不到火把倒影；`LakeReflection` 已经调用 `ReflectionProbe.RenderProbe()`，也没有任何报错。
- 定位：湖面材质反照率接近黑、光滑度 0.96，看到的颜色几乎全部来自反射探针，所以问题在探针。检查 `ProjectSettings/QualitySettings.asset`，4 个级别全部是 `realtimeReflectionProbes: 0`（模板默认值）。
- 根因：这个开关关闭时，引擎会静默跳过所有 Realtime 模式的探针，包括 `ViaScripting` 刷新的探针。调用不报错，只是什么也不发生。
- 修复：`ProjectSetup` 为所有档位打开该开关。性能差异改由 `LakeReflection` 控制：Ultra 每 4 帧分时刷新（每次渲染 1 个面），其余档位只在启动和切档时渲染一次。
- 验证：固定机位 l01、l02、l04、l05 重新截图，湖面出现火把倒影。
- 教训：功能代码正确，但被工程级开关静默关闭。这类问题只靠单元测试发现不了，需要固定机位截图对比来兜底。
