# 第三方素材

| 素材 | 来源 | 许可证 | 工程内位置 | 用途 |
| --- | --- | --- | --- | --- |
| Mines and Cave Set 1.0（Loafbrr） | Unity Asset Store（免费） | CC0 1.0（包内 `README.txt`） | `Assets/LoafbrrAssets/` | 矿道、洞穴、地面、木结构、铁轨、矿车 |
| Starter Assets – First Person + Third Person Character Controllers 2.0.2（Unity Technologies） | Unity Asset Store（免费） | Unity Companion License | `Assets/Starter Assets/` | 玩家角色模型、动画、动画控制器 |

## 导入方式

两个包都用 `tools/extract_unitypackage.py` 解包，而不是 Unity 的 Import：

- Starter Assets 包里带了 `Packages/manifest.json`，正常导入会整份覆盖本工程的包清单（会把 Input System 1.20.1 降到 1.16.0，并删掉测试框架等）。
- 排除的内容：两个包的示例场景、Starter Assets 的教程（TutorialInfo）、矿洞包的 Blender 源文件。
- 导入后删除：Starter Assets 的 `Editor/URPWizard`（每次打开工程都会运行，在没有渲染管线时把找到的第一个 URP 资源设为默认）和 `Runtime/Settings` 下的 6 套 URP 管线资源（没有任何引用）。
- 角色包需要的 Cinemachine 3.1.7 手动加进 `Packages/manifest.json`。

```bash
python3 -I tools/extract_unitypackage.py "<包路径>.unitypackage" . --exclude "Assets/Starter Assets/Sample" --exclude "Assets/Starter Assets/TutorialInfo"
```
