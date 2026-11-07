# 平台差异案例库

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
