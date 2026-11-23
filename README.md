# 矿坑余烬 · MineEmbers

Unity 6 (6000.3.28f1 LTS) · URP Forward+ · Metal / D3D12 / Vulkan · C# + C++ + Python

一个约 15 分钟的第三人称地下矿坑关卡（矿坑入口 → 地下湖 → 熔炉大厅），**用来承载一套图形平台工具链**：GPU 能力探测、画质分级与自动选档、自动化性能采集与回归对比、PSO 预热。游戏本身是“被测对象”，工具链才是重点。

| 矿坑入口 | 地下湖 | 熔炉大厅 |
| --- | --- | --- |
| ![入口](docs/images/e04.jpg) | ![湖](docs/images/l02.jpg) | ![熔炉](docs/images/f02.jpg) |

## 快速运行

### 方式一：下载预编译包（不需要装 Unity）

到本仓库的 [Releases](../../releases) 下载：

- **macOS**（Apple 芯片和 Intel 通用）：`MineEmbers-mac.zip` 解压后，包没有经过 Apple 公证，第一次运行前在终端执行一次：

  ```bash
  xattr -dr com.apple.quarantine MineEmbers.app
  ```

  然后双击打开。
- **Windows**（x64，D3D12）：`MineEmbers-windows.zip` 解压后运行 `MineEmbers.exe`。注意：Windows 版的 GpuCaps 原生插件 **还没有在 Windows 上编译**，能力探测会自动降级为 Unity 自带的 `SystemInfo`（能力报告里 `"source": "systeminfo"`，Mac 上为 `"native"`），其余功能不受影响。

操作：`WASD` 移动、鼠标转视角、`Shift` 跑、`空格` 跳；`Esc` 设置菜单（切换画质档位、重新检测、查看 API / GPU / 驱动），`F3` 性能 HUD。

不想手动玩，可以直接看自动化功能（macOS 示例，Windows 把路径换成 `MineEmbers.exe`）：

```bash
APP=MineEmbers.app/Contents/MacOS/MineEmbers
$APP -perfrun -tier=high -perf-out "$PWD/perf"     # 自动沿固定路线跑图，输出帧时间 CSV 后退出（约 1 分钟）
$APP -shots "$PWD/shots" -tier=high                # 15 个固定机位截图后退出（画面确定，可做像素对比）
$APP -caps-only -caps-out "$PWD/caps.json"         # 只输出 GPU 能力报告（JSON）
```

### 方式二：从源码打开

1. 安装 **Git LFS**（贴图、模型、原生插件二进制都在 LFS 里，不装的话会是指针文件）：`git lfs install`
2. `git clone https://github.com/zhufengwang1997-lab/MineEmbers.git`
3. 用 Unity Hub 安装 **6000.3.28f1**，打开工程（第一次导入约 3–5 分钟）
4. 打开 `Assets/Scenes/Mine.unity`，点 Play

Python 工具只依赖标准库：`python3 tools/test_tools.py` 跑工具自测。

## 看点

| 方向 | 做了什么 | 证据 |
| --- | --- | --- |
| 原生插件 / 多图形 API | C++ 插件通过 Unity 原生接口拿到 Metal / D3D12 / Vulkan 设备，按 API 查询光追、Mesh Shader、VRS、Bindless 等能力，输出统一 JSON | [NativePlugins/GpuCaps](NativePlugins/GpuCaps)、[docs/caps](docs/caps) |
| 画质分级 | 4 档 URP 配置由代码生成（配置即代码）；自动选档 = 硬件上限 × 实测帧时间 × 兼容规则上限 | [Packages/com.zfw.qualitytiers](Packages/com.zfw.qualitytiers)、[Assets/Editor/ProjectSetup.cs](Assets/Editor/ProjectSetup.cs) |
| 性能回归 | 命令行跑固定路线，按区域采集 CPU / GPU 帧时间、Draw Call、GC；`compare.py` 按平台阈值判定回退，退出码可直接接 CI | [Packages/com.zfw.perfrunner](Packages/com.zfw.perfrunner)、[docs/perf](docs/perf/README.md) |
| PSO 预热 | Unity 6 GraphicsStateCollection 录制 + 启动预热：冷启动入口峰值 71 ms → 4 ms，1% Low +60% | [docs/perf/README.md](docs/perf/README.md#pso-预热实验) |
| 问题定位 | 10 个案例，每个都有现象、复现、排除过程、根因、验证数据 | [docs/cases](docs/cases/README.md) |
| 可复用 | 三个模块是独立的 UPM 包，另一个项目通过 Git URL 引用 | `Packages/com.zfw.*` |

推荐先读的案例：

- [CASE-05](docs/cases/CASE-05-point-shadow-atlas-overflow.md)：阴影图集溢出，High 档反而比 Medium 糊。
- [CASE-07](docs/cases/CASE-07-frame-timing-double-count.md)：测量工具自己把帧时间算重，差点得出“PSO 预热有害”的错误结论。
- [CASE-08](docs/cases/CASE-08-lake-hitch-and-runner-garbage.md)：用排除法定位偶发卡顿，并把测量工具自身的 GC 从每次 38 次降到 5 次。

## 源码从哪里读起

| 想看 | 文件 |
| --- | --- |
| 启动流程（选档 → 预热 → 进入模式） | [Assets/Scripts/Boot/GameBootstrap.cs](Assets/Scripts/Boot/GameBootstrap.cs) |
| 原生插件入口与各 API 后端 | [NativePlugins/GpuCaps/src](NativePlugins/GpuCaps/src) |
| C# 侧 P/Invoke 与降级 | [Packages/com.zfw.gpucaps/Runtime/GpuCaps.cs](Packages/com.zfw.gpucaps/Runtime/GpuCaps.cs) |
| 选档逻辑 | [Packages/com.zfw.qualitytiers/Runtime/TierSelector.cs](Packages/com.zfw.qualitytiers/Runtime/TierSelector.cs) |
| 帧时间采集（每帧零分配） | [Packages/com.zfw.perfrunner/Runtime/PerfRunner.cs](Packages/com.zfw.perfrunner/Runtime/PerfRunner.cs) |
| 回归判定 | [Packages/com.zfw.perfrunner/Tools~/compare.py](Packages/com.zfw.perfrunner/Tools~/compare.py) |
| 点光源阴影预算 | [Assets/Scripts/Rendering/TierLightManager.cs](Assets/Scripts/Rendering/TierLightManager.cs) |
| 自定义渲染 Pass（热浪扭曲） | [Assets/Scripts/Rendering/HeatHazeFeature.cs](Assets/Scripts/Rendering/HeatHazeFeature.cs) |
| 关卡生成器（场景由代码生成，不手改） | [Assets/Editor/LevelBuilder.cs](Assets/Editor/LevelBuilder.cs)、[Assets/Editor/LevelArt.cs](Assets/Editor/LevelArt.cs) |

## 完成情况

| 项 | 状态 |
| --- | --- |
| macOS（Apple M5 Max · Metal）：四档基线、PSO 预热实验、噪声与回归阈值、换素材前后对比 | ✅ 完成 |
| 游戏：三个区域、91 盏动态火光、粒子、后处理、湖面反射、热浪、正式美术素材、角色动画 | ✅ 完成 |
| 工具：能力报告、性能采集与对比、截图差异、汇总表、自测 | ✅ 完成 |
| Windows 包（Mono，在 Mac 上交叉编译） | ✅ 可运行；GpuCaps.dll 源码已写好，**未在 Windows 上编译** |
| Windows（GTX 1070 Ti）D3D12 / Vulkan 实测、跨 API 截图对比、优化项 OPT-01…05 | ⏳ 未做，操作手册见 [docs/windows.md](docs/windows.md) |
| 1% Low 在空闲机器上补测（素材版本） | ⏳ 见 [CASE-09](docs/cases/CASE-09-concurrent-build-contamination.md) |

素材来源与许可证见 [THIRD_PARTY.md](THIRD_PARTY.md)。

---

# 技术参考

## 目录

```text
Assets/                 游戏内容（场景、美术、脚本）
Packages/com.zfw.*      三个可复用的 UPM 包（项目三通过 Git URL 引用）
NativePlugins/GpuCaps/  插件 C++ 源码与 CMake（macOS .bundle / Windows .dll）
tools/                  开发脚本与工具自测
docs/                   caps/ perf/ cases/ captures/ —— 能力报告、性能数据、案例库、抓帧
```

## 常用命令

```bash
# 工具自测（只依赖 Python 标准库）
python3 tools/test_tools.py

# 性能对比：退出码 0 = 通过，1 = 回退，2 = 条件不一致
python3 "Packages/com.zfw.perfrunner/Tools~/compare.py" base.csv new.csv -o report.html   # 阈值按平台自动选（Tools~/thresholds.json），--threshold 可覆盖

# 能力矩阵
python3 "Packages/com.zfw.gpucaps/Tools~/caps_merge.py" docs/caps/*.json -o docs/caps/matrix.html

# 截图差异
python3 "Packages/com.zfw.perfrunner/Tools~/screenshot_diff.py" shots_dx12 shots_vk -o diff.html
```

## 启动参数

| 参数 | 说明 |
| --- | --- |
| `-force-d3d12` / `-force-vulkan` | 选择图形 API（Windows） |
| `-tier=<low\|medium\|high\|ultra>` | 跳过自动选档 |
| `-caps-out <路径>`、`-caps-only` | 能力报告位置；只采集能力后退出 |
| `-perfrun [-perf-out <路径>] [-perf-seconds 20]` | 跑图模式 |
| `-shots <目录>` | 15 个固定机位截图后退出（画面确定：火光冻结、粒子固定种子、热浪固定时间） |
| `-perf-warmup <秒>` | 跑图前的预热时长（冷启动实验用 0） |
| `-gsc-record` | Development 包录制 PSO（GraphicsStateCollection），退出时存到 persistentDataPath |

运行中：`Esc` 设置菜单（切档、重新检测、API/GPU/驱动/兼容案例），`F3` 性能 HUD。

## 构建与测量（Mac）

```bash
tools/unity.sh MineEmbers.EditorTools.LevelBuilder.Build          # 重新生成关卡 Assets/Scenes/Mine.unity（加 -greybox 生成白模）
tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildMac [-dev]  # -> Builds/Mac/MineEmbers.app
tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildWindows     # Mono 交叉编译 -> Builds/Windows/
tools/run_mac_suite.sh                                             # 冷缓存预热实验 + 4 档性能
python3 tools/perf_summary.py out/perf/*.csv                       # 按 设备/API/档位/区域 汇总成 Markdown 表
python3 tools/contact_sheet.py out/shots -o sheet.png              # 15 个机位拼成一张总览图
```

`tools/unity.sh` 会在非编译错误时自动重试一次（见 CASE-03）。Windows 测试机流程见 [docs/windows.md](docs/windows.md)，性能数据见 [docs/perf/README.md](docs/perf/README.md)。

## 工程配置即代码

`Assets/Editor/ProjectSetup.cs` 生成 4 档 URP Asset / TierSettings / Quality 级别，并设置图形 API（Win: D3D12 + Vulkan，Mac: Metal）与 Frame Timing Stats。改参数后重跑：

```bash
/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -executeMethod MineEmbers.EditorTools.ProjectSetup.Run
```

EditMode 测试（不加 `-nographics`，才会有真实的图形设备让插件回答）：

```bash
/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
```
