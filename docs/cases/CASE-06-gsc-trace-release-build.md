# CASE-06 GraphicsStateCollection 在 Release 包里“录制成功”却是空的

- 平台：macOS · Metal · Unity 6000.3.28f1
- 现象：带 `-gsc-record` 跑一遍 Release 包，`BeginTrace()` 返回 true，`SaveToFile()` 也返回 true，但文件里是 **0 个状态 / 0 个变体**。没有任何警告。
- 定位：同样的场景和路线改用 Development 包，录到 33 个状态 / 33 个变体。
- 根因：PSO 追踪依赖 Development Player 的运行时钩子；Release 包里追踪是空操作。API 本身不会报告这一点。
- 处理（工作流）：
  1. 用 `BuildScript.BuildMac -dev` 打 Development 包；
  2. 每个档位各跑一遍 `-perfrun -tier=<t> -gsc-record`。档位的 URP Asset 不同（MSAA、深度纹理、不透明纹理），PSO 也不同；
  3. 把结果拷进 `Assets/StreamingAssets/gsc_<API>_<tier>.graphicsstate`；
  4. Release 包启动时 `ShaderWarmup` 加载当前 API 的全部集合并 `WarmUp()`。
  - PSO 和图形 API 相关，所以 D3D12、Vulkan 需要在 Windows 机器上各自录一份。
- 验证：见 `docs/perf/README.md` 的“PSO 预热实验”。为了避免驱动缓存掩盖结果，每次运行前都清空 Metal 着色器缓存 `$(getconf DARWIN_USER_CACHE_DIR)/com.zfw.mineembers`。
- 教训：新 API（这里是 `Experimental` 命名空间下的接口）“没报错”不等于“生效了”。要用可观测的数字（状态数、峰值帧时间）来验收。
