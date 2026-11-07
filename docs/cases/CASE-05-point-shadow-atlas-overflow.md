# CASE-05 档位越高阴影反而越糊：点光源阴影图集溢出

- 平台：全平台（在 Apple M5 Max · Metal 上发现）· URP 17.3.0 · Forward+
- 发现方式：PerfRunner 跑完后检查 Player.log，四个档位都有同一条警告（2026-11-07）：

```text
Reduced additional punctual light shadows resolution by 4 to make 24 shadow maps fit in the 2048x2048 shadow atlas
Reduced additional punctual light shadows resolution by 4 to make 48 shadow maps fit in the 2048x2048 shadow atlas   (High)
Reduced additional punctual light shadows resolution by 4 to make 90 shadow maps fit in the 4096x4096 shadow atlas   (Ultra)
```

- 根因：点光源阴影是立方体阴影，每盏灯要往 Additional Lights 阴影图集里写 6 个面。初版档位表只规定了“投影灯数量”和“阴影分辨率”，没有把两者联系起来：
  - High：8 盏 × 6 面 = 48 张，每张 1024²，需要 50 M 像素；图集只有 2048²，即 4.2 M 像素。
  - URP 不报错，而是把所有阴影的分辨率统一降低，直到放得下。结果 High 的单张阴影分辨率反而比 Medium 低。
- 修复：`ProjectSetup` 改为同时规定“图集大小”和“单张分辨率”，并用断言保证 `灯数 × 6 × 单张² ≤ 图集²`：

| 档位 | 投影灯 | 单张 | 图集 | 占用 / 容量 |
| --- | --- | --- | --- | --- |
| Low | 2 | 256 | 1024 | 0.79 M / 1.05 M |
| Medium | 4 | 256 | 2048 | 1.57 M / 4.19 M |
| High | 8 | 512 | 4096 | 12.6 M / 16.8 M |
| Ultra | 16 | 512 | 8192 | 25.2 M / 67.1 M（16 位深度约 128 MB） |

- 代价：Ultra 的 8192² 图集要占约 128 MB 显存。显存 < 4 GB 的设备会被选档上限限制在 Medium，用不到 Ultra。
- 验证：修复后重跑四个档位，日志中这条警告的出现次数为 0（`tools/run_mac_suite.sh` 会打印每个档位的计数）。
- 教训：**档位表里的参数之间有约束，要写成断言，不能只是并列的几个数字**。另外引擎在日志里打印的“降级”警告，要当作测试失败对待。
