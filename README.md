# 矿坑余烬 · MineEmbers

Unity 6 (6000.3 LTS) · URP Forward+ · D3D12 / Vulkan / Metal

一个约 15 分钟的地下矿坑小关卡（矿坑入口 → 地下湖 → 熔炉大厅），作为图形平台工具链的试验场：

| 模块 | 作用 | 位置 |
| --- | --- | --- |
| GpuCaps | C++ Native Plugin，按 API 探测 GPU 能力，输出 JSON | `Packages/com.zfw.gpucaps`、`NativePlugins/GpuCaps` |
| QualityTiers | 4 档画质 + 自动选档 + 兼容回退表 | `Packages/com.zfw.qualitytiers` |
| PerfRunner | 命令行跑图、采集帧时间 CSV、对比报警 | `Packages/com.zfw.perfrunner` |

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
python3 "Packages/com.zfw.perfrunner/Tools~/compare.py" base.csv new.csv -o report.html --threshold 0.05

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
tools/unity.sh MineEmbers.EditorTools.LevelBuilder.Build          # 重新生成白模关卡 Assets/Scenes/Mine.unity
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
