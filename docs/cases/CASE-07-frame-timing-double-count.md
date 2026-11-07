# CASE-07 帧时间被重复计数：1 秒“卡顿”其实是启动帧

- 平台：Metal / Apple M5 Max / Unity 6000.3.28f1（与 API 无关，FrameTimingManager 的通用行为）
- 现象：冷缓存预热实验里，矿坑入口“入区峰值”有 GSC 1150 ms、无 GSC 1020 ms——加了 PSO 预热反而更糟；1% Low 只有 31–33 fps。
- 复现：`tools/run_mac_suite.sh warmup`（`-perfrun -tier=high -perf-warmup 0`，每次运行前清空 Metal 着色器缓存）

## 定位

逐帧看 CSV 前 12 行：

| 行 | cpu_ms | gpu_ms | draw_calls |
| ---: | ---: | ---: | ---: |
| 0 | **1020.429** | — | **29124** |
| 1 | 8.418 | — | 0 |
| 2 | 1.401 | — | 947 |
| 3 | **1020.384** | 5.645 | 898 |
| 9 | 71.197 | 2.969 | 868 |
| 10 | 49.208 | 1.223 | 868 |

- 第 0 行的 29124 个 Draw Call 不可能是一帧画出来的：ProfilerRecorder 的 `LastValue` 在第一次采样时是**启动以来的累计值**。
- 第 0 行和第 3 行 CPU 时间几乎完全相同：同一个帧被记了两次。
  - 第 0 行：`GetLatestTimings` 还没有新结果，代码退回 `Time.unscaledDeltaTime`——那是**上一帧**（场景加载 + PSO 预热那一帧）的时长；
  - 第 3 行：GPU 做完那一帧后，FrameTimingManager 把它的时间正式交出来，再记一次。
- 两次运行的这个“尖刺”都 ≈ 1 秒，而真正的 PSO 编译卡顿（第 9、10 行）只在无 GSC 时出现——说明 1 秒那帧是启动阶段（场景加载、反射探针首次渲染、GSC 预热），不是游戏中的卡顿。

## 根因

FrameTimingManager 的数据**晚几帧**才到（Metal / D3D12 / Vulkan 都有 2–3 帧在途），而且连续调用可能返回同一个已完成帧。原实现：
1. 开始采集时没有丢弃启动帧；
2. 没有按帧去重；
3. 拿不到新数据时用 `unscaledDeltaTime` 补位，而它和稍后到达的 FrameTiming 是同一帧。

## 修复

`Packages/com.zfw.perfrunner/Runtime/PerfRunner.cs`：
- 开始前丢弃 `max(maxQueuedFrames,2)+4` 帧，确保启动帧的时间已经“流过去”；
- 用 `FrameTiming.frameStartTimestamp` 去重：每个时间戳只用一次；
- 没有新帧时该行 CPU/GPU 留空（分析工具按缺失值处理）；只有平台完全不支持 Frame Timing 时才退回 `unscaledDeltaTime`。

## 验证

修复后同一实验（各 3 次冷启动）：启动帧不再出现在 CSV 中，剩下的 >16.7 ms 帧只有真实的 PSO 编译——无 GSC 时入口 ~71 ms / ~48 ms + 熔炉 ~20 ms，有 GSC 时为 0–1 个。数据见 [docs/perf/README.md](../perf/README.md#pso-预热实验)。

## 教训

- “数字离谱”先看原始逐帧数据，再下结论；一个 1 秒的尖刺会把 1% Low 从 400 fps 拉到 30 fps。
- 异步回读的计时数据（GPU 时间戳、FrameTimingManager、Profiler 计数器）都要考虑**延迟**与**去重**；同一行里混用“当前帧”与“N 帧前”的数据要非常小心。
