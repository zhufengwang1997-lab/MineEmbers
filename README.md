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
| `-shots <目录>` | 15 个固定机位截图后退出 |
