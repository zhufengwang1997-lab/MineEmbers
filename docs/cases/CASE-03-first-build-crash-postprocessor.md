# CASE-03 生成资源后的第一次构建，编辑器在 AssetPostprocessor 里崩溃

- 平台：macOS 26.4.1 · Unity 6000.3.28f1 · 命令行 `-executeMethod BuildScript.BuildMac`
- 现象：退出码 138（SIGBUS），日志里只有 256 帧没有符号的调用栈。同一命令立即重跑一次就成功（2026-11-07）。
- 定位：
  - 编辑器日志只能说明崩溃发生在 `build_commit.txt` 导入之后、`BuildPlayer` 开始时。
  - 系统崩溃报告 `~/Library/Logs/DiagnosticReports/Unity-*.ips` 给出了原生调用栈：`AssetDatabase::SaveAssetIfDirty` → `ImportAssets` → `MonoPostprocessAllAssets`（托管 AssetPostprocessor）→ 分配字符串时 GC 崩溃。
  - 托管侧调用 `SaveAssetIfDirty` 的只有包内代码（URP 的 `ShaderBuildPreprocessor` 等），本项目没有自定义 AssetPostprocessor。
- 根因（推断）：刚生成大量资源（材质、场景、Renderer Feature 子资产）后，构建前的“保存脏资源”触发包内 postprocessor 重入。第二次运行时资源已是干净状态，不再触发。
- 处理：`tools/unity.sh` 对非编译错误的失败自动重试一次；编译错误（`error CS`）不重试，直接报错。
- 教训：命令行构建失败先看系统崩溃报告，编辑器日志常常只有无符号栈。“重试一次”只适用于已经定位过、确认是偶发的问题，并且要记录在案。
