# CASE-10 接入商店素材：一个包会改掉整个工程的配置

- 平台：Unity 6000.3.28f1 / URP 17.3 / 所有平台
- 现象：准备导入 *Starter Assets – Third Person*（角色）和 *Mines and Cave Set*（环境）两个免费包时，检查包内容发现三处会悄悄改坏工程的东西。
- 复现：`tar -tzf <包>.unitypackage`，再读每个条目的 `pathname`。

## 发现

| 问题 | 后果（如果直接点 Import） | 处理 |
| --- | --- | --- |
| 角色包里带了一份 `Packages/manifest.json` | 整份覆盖本工程的包清单：Input System 1.20.1 降到 1.16.0，测试框架和三个自研包的引用被删掉 | 不用 Unity 的 Import，改用 `tools/extract_unitypackage.py` 解包，只写 `Assets/` 下的文件 |
| 角色包的 `Editor/URPWizard` 带 `[InitializeOnLoadMethod]` | 每次打开工程都运行，发现“没有渲染管线”时把找到的第一个 URP 资源设成默认——本工程的管线是按档位在运行时切换的，这会干扰档位系统 | 删除该目录，以及 `Runtime/Settings` 下 6 套没有引用的 URP 管线资源 |
| 环境包里有 Blender 源文件和示例场景 | 多 100 MB 以上进 Git LFS，示例场景还会被构建扫描 | 解包时排除 |

## 模块化套件的摆放

环境包是“拼装件”（墙、地、天花板、立柱、横梁、铁轨），每个模型的原点和朝向都不统一。生成器 `Assets/Editor/LevelArt.cs` 的做法：

- **按实测包围盒对齐**：`Snap(物体, 目标点, 锚点)`，锚点用包围盒范围内的 [-1, 1]³ 坐标表示（例如“底面中心”是 (0, -1, 0)），不依赖模型原点。
- **按网格法线判断正面**：把所有三角形的法线按面积加权求平均，再吸附到最近的坐标轴，得到“这块墙的正面朝哪”。
- **只绕 Y 轴旋转**：用 `Vector3.SignedAngle` 算偏航角。`Quaternion.FromToRotation` 在两个向量正好相反时可能绕任意轴转 180°，把墙翻成倒立。
- 相邻两块交替偏移 1–2 cm，避免共面闪烁（Z-fighting）。

## 对性能的保护

- 白模方块保留为**看不见的碰撞体**：玩法、15 个机位、性能区域（PerfZone）都不变，换素材前后的数据可以直接对比。
- 地面和天花板关闭阴影投射，点光源阴影少画一大批物体。
- 所有拼装件打上 Static 标记（合批、遮挡剔除、反射探针）。
- `-greybox` 参数仍可生成旧白模关卡，用于对比。

## 教训

- 商店包是**第三方代码**，导入前要像审代码一样看一遍：有没有 manifest、`[InitializeOnLoad]`、会改 ProjectSettings 的编辑器脚本。
- 素材来源和许可证记在 [../../THIRD_PARTY.md](../../THIRD_PARTY.md)。
