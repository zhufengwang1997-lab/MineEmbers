# CASE-08 地下湖偶发卡顿：排除法，以及顺带发现“测量工具自己在制造垃圾”

- 平台：Metal / Apple M5 Max / High 档 / Mono 后端
- 现象：冷启动实验里，地下湖区域约一半的运行会出现**一帧** 12–26 ms 的卡顿；有没有 GSC 都会出现。
- 复现：`-perfrun -tier=high -perf-warmup 0 -perf-seconds 8`，重复 5 次左右能看到 2 次。

## 定位过程（排除法）

| 假设 | 检验 | 结论 |
| --- | --- | --- |
| PSO 编译 | 有 GSC 预热时照样出现 | ❌ 排除 |
| GPU 负载突增 | 卡顿帧的 gpu_ms、Draw Call、SetPass 都和前后帧一样 | ❌ 只在 CPU 侧 |
| 场景里某个物体（进入视野、首次模拟） | 卡顿出现在湖区路径的 51% / 65% / 10% 处，位置不固定 | ❌ 与位置无关 |
| GC（垃圾回收） | 给 CSV 加 `gc` 列（`GC.CollectionCount(0)` 是否变化），卡顿帧离最近一次 GC 有 9 / 161 帧，GC 本身那一帧只有 ~1 ms | ❌ 排除 |
| 操作系统调度 / 窗口服务器 | 需要 Development 包 + Profiler / Instruments 看主线程 | ⏳ 待查 |

结论：这是一个 **≈ 0.4 次/运行、只影响 CPU 的一帧卡顿**，不是渲染问题。对区域平均值影响可以忽略（湖区一次运行约 7000 帧），但会影响 1% Low。先记录，换上正式素材或到 Windows 上复现后再决定是否深挖。

## 顺带发现：测量工具自己在产生垃圾

加了 `gc` 列后看到：**每 ~600 帧就有一次 GC**（一次 24 秒的运行 38 次）。来源：

1. **PerfRunner 每帧把数字格式化成字符串**（`double.ToString` 等）再拼进 StringBuilder——测量工具本身就在扰动被测对象（观察者效应）。
2. **设置菜单的 `OnGUI`**：即使直接 return，引擎每帧仍会为 IMGUI 的 Layout 事件分配内存；HUD 文字每帧重新拼字符串。
3. `List.Sort(Comparison)` 每次调用都会包一层比较器对象（每秒 4 次，影响很小）。

修复：

| 修改 | 位置 |
| --- | --- |
| 每帧只往预分配的 `List<Sample>`（结构体）里写原始数字，结束时一次性格式化成 CSV | `Packages/com.zfw.perfrunner/Runtime/PerfRunner.cs` |
| 菜单关闭时 `useGUILayout = false`；HUD 文字每秒更新 4 次 | `Assets/Scripts/Boot/SettingsMenu.cs` |
| 排序改用缓存的 `IComparer` | `Assets/Scripts/Rendering/TierLightManager.cs` |

## 验证

同样条件（High，8 秒/区域，约 23,000 帧）：

| 阶段 | 每次运行的 GC 次数 |
| --- | ---: |
| 修复前 | 38 |
| 只改 PerfRunner | 27 |
| 再改菜单和排序 | **5** |

剩下的 5 次来自引擎 / URP 内部。湖区卡顿在修复后仍然出现（3 次中 2 次），再次说明它和 GC 无关。

## 教训

- 排除法要**每一步都有数据**：每个假设对应一个可以证伪的检验。
- 给测量工具加诊断列（这里是 `gc`）往往比猜更快，而且会顺带暴露别的问题。
- 测量工具必须“安静”：每帧零分配，所有格式化、写文件都放到采集结束之后。
