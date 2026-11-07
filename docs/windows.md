# Windows 测试机操作手册（GTX 1070 Ti）

Mac 上完成开发与 Mono 交叉编译；这台机器负责：编译 `GpuCaps.dll`、跑 D3D12 / Vulkan 的能力报告、性能基线、截图对比，以及 RenderDoc / Nsight 抓帧。

## 0. 一次性准备

| 软件 | 用途 | 说明 |
| --- | --- | --- |
| Git + Git LFS | 拉代码 | `git lfs install` 后再 clone，否则 `.bundle`/`.dll` 只是指针文件 |
| Visual Studio 2022（“使用 C++ 的桌面开发”工作负载） | 编译插件 | 自带 CMake 和 Windows SDK（含 D3D12 / DXGI 头文件） |
| Vulkan SDK（LunarG） | 只用到头文件 | 安装后 `VULKAN_SDK` 环境变量会被 CMake 的 `find_package(Vulkan)` 找到 |
| Unity Hub + 6000.3.28f1 | 可选 | 只在需要 IL2CPP 构建或在 Windows 上开编辑器时才装（“Windows Build Support (IL2CPP)” 模块） |
| Python 3.10+ | 跑对比工具 | 只用标准库，无需 pip |
| RenderDoc、NVIDIA Nsight Graphics | 抓帧 | M5 阶段使用 |
| 显卡驱动 | — | 记下版本号（如 566.36），写进每次结果的说明 |

```powershell
git clone https://github.com/zhufengwang1997-lab/MineEmbers.git
cd MineEmbers
git lfs pull
```

## 1. 编译 GpuCaps.dll

需要 Unity 的插件头文件。两种来源任选其一：
- 装了 Unity：`C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Data\PluginAPI`
- 没装 Unity：从 Mac 拷贝 `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/PluginAPI` 整个文件夹到本机任意位置

在“x64 Native Tools Command Prompt for VS 2022”里：

```bat
cd NativePlugins\GpuCaps
cmake -S . -B build\win -G "Visual Studio 17 2022" -A x64 -DUNITY_PLUGIN_API="C:\path\to\PluginAPI"
cmake --build build\win --config Release
```

产物会直接写到 `Packages\com.zfw.gpucaps\Plugins\Windows\GpuCaps.dll`。检查导出函数：

```bat
dumpbin /exports ..\..\Packages\com.zfw.gpucaps\Plugins\Windows\GpuCaps.dll
```

应只看到 4 个：`GpuCaps_GetRendererId`、`GpuCaps_GetReportJson`、`UnityPluginLoad`、`UnityPluginUnload`。然后提交 DLL（走 LFS）：

```bat
git add Packages/com.zfw.gpucaps/Plugins/Windows/GpuCaps.dll
git commit -m "GpuCaps: Windows x64 build (VS2022)"
git push
```

> 第一次提交后，在 Mac 上重新打 Windows 包（`tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildWindows`）DLL 才会进包。在那之前，Windows 包里的 GpuCaps 会自动降级到 `SystemInfo`（日志里 `source=systeminfo`），其余功能不受影响。

## 2. 拿到 Windows 包

Mac 上已经交叉编译好 Mono 版本：`Builds/Windows/`（不进 git）。拷贝方式任选：U 盘、局域网共享、或者把文件夹压缩后放网盘。整个文件夹都要（`MineEmbers.exe`、`MineEmbers_Data`、`UnityPlayer.dll` 等）。

## 3. 每次测试的固定流程

在 PowerShell 里，进入游戏目录。**测试前**：插电源、关掉浏览器和下载、NVIDIA 控制面板“电源管理模式 = 最高性能优先”、垂直同步由游戏自己关闭（PerfRunner 会设 `vSyncCount = 0`）。

```powershell
$out = "$PWD\results"; mkdir $out -Force
# 能力报告（两个 API 各一次）
.\MineEmbers.exe -force-d3d12  -caps-only -caps-out $out -logFile $out\caps_dx12.log
.\MineEmbers.exe -force-vulkan -caps-only -caps-out $out -logFile $out\caps_vk.log
# 性能：每个 API × 档位跑一次（约 70 秒/次）
foreach ($api in "d3d12","vulkan") { foreach ($t in "low","medium","high","ultra") {
  .\MineEmbers.exe -force-$api -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 `
    -perfrun -tier=$t -perf-out $out -logFile "$out\perf_${api}_$t.log"
}}
# 截图（两个 API 各一次，用于跨 API 画面差异）
.\MineEmbers.exe -force-d3d12  -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -tier=high -shots $out\shots_dx12 -logFile $out\shots_dx12.log
.\MineEmbers.exe -force-vulkan -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -tier=high -shots $out\shots_vk   -logFile $out\shots_vk.log
```

然后把整个 `results` 文件夹交回（放进仓库的 `docs/caps/`、`docs/perf/windows/`，或者直接发给我）。

## 4. 在本机直接看结果（可选）

```powershell
python Packages\com.zfw.gpucaps\Tools~\caps_merge.py results\caps_*.json -o results\matrix.html
python Packages\com.zfw.perfrunner\Tools~\compare.py results\perf_<commit>_Direct3D12_high.csv results\perf_<commit>_Vulkan_high.csv -o results\dx12_vs_vk.html
python Packages\com.zfw.perfrunner\Tools~\screenshot_diff.py results\shots_dx12 results\shots_vk -o results\shots_diff.html
```

注意：`compare.py` 对比 DX12 和 Vulkan 两份 CSV 时会**拒绝**（退出码 2，因为 api 不同）——这是设计如此：它是“同条件回归检测”工具。跨 API 对比请看 `docs/perf/` 里的汇总表（由 `tools/perf_summary.py` 生成）。

## 5. 自动选档验证

不带 `-tier` 直接双击运行一次：首次启动会显示 “Detecting graphics performance...” 5 秒，然后自动选档。按 Esc 打开设置菜单可以看到选档方式（Auto）、API、GPU、驱动。再次启动不会重复检测（存档的 GPU + 驱动未变）；更新驱动后会重新检测。

日志位置：`%USERPROFILE%\AppData\LocalLow\zfw\MineEmbers\Player.log`，搜索 `[TierBenchmark]`，那一行列出了显存、是否独显、上限、基准帧时间和最终档位。
