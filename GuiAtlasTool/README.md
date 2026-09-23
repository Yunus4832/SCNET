# GUI 图集源文件

`Assets/` 中的每个 SVG 或 PNG 都是一个完整、独立且背景透明的图集条目。文件名会直接成为
`Textures/Atlas/` 后的资源名。

源文件要求：

- SVG 使用标准格式，不依赖外部图片、字体或其他工程文件；
- SVG 根元素使用整数像素声明 `width` 和 `height`，并提供与画布对应的 `viewBox`；
- PNG 使用未预乘 Alpha 的 RGBA 数据，生成器会在输出图集时统一预乘一次；
- 不包含编辑器背景、隐藏设计图层或页面底色；
- 同一条目不能同时提供 SVG 和 PNG；普通和按下状态分别保存，例如 `PlayerList.svg` 与
  `PlayerList_Pressed.svg`。

新增 GUI 资产默认优先使用 SVG，以便保持几何结构可编辑并获得可控的缩放效果。只有小尺寸图标
在最终尺寸下经过视觉比较，确认 PNG 的像素对齐、线宽或抗锯齿效果更好时，才优先保留 PNG。
Inkscape 工程、下载资源或其他绘图工具可以作为源文件的上游，但不属于图集生成协议。
`Assets/` 中的独立 SVG 和 PNG 才是仓库中的权威源文件。

该工具只生成 GUI 图集。必须按名称单独加载、生命周期不同、尺寸较大或需要独立采样参数的
纹理继续保留在 `Content/Assets/Textures/` 中。

生成命令和构建期增量行为见 [`Doc/BuildAndConfig.md`](../Doc/BuildAndConfig.md)。

## PNG 预处理

普通未预乘 PNG 可以直接标准化到源目录：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  input.png GuiAtlasTool/Assets/EntryName.png
```

从旧版游戏资源恢复的 PNG 如果已经预乘 Alpha，增加 `--premultiplied`，工具会在保存源资产前
反预乘：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  input.png GuiAtlasTool/Assets/EntryName.png --premultiplied
```

从已生成的预乘图集中提取一个或多个条目时，使用 `extract`。该命令读取映射、裁切并自动
反预乘：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- extract \
  AtlasTexture.png Atlas.txt GuiAtlasTool/Assets Entry1 Entry2
```

预处理命令默认拒绝覆盖已有文件；明确需要替换时增加 `--overwrite`。

## 组合游戏内 HUD 操作按钮

`compose-game-button` 只用于组合玩家在游戏过程中使用的 HUD 操作按钮纹理，例如库存、天气、
骑乘和潜行按钮。它不用于通用 Widget，也不用于 Screen、Dialog 或 ActionPanel 中由 UI 样式和
布局系统生成的按钮。

命令将一个独立图标 SVG 与游戏内按钮边框组合，同时生成普通态和按下态，并默认写入
`GuiAtlasTool/Assets/`：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- compose-game-button \
  floating path/to/Icon.svg NewButton
```

支持 `floating`、`left-attached` 和 `right-attached` 三种边框。边框模板位于
`GuiAtlasTool/Templates/Buttons/`，是从 Inkscape 工程导出的稳定工具资源；组合过程不依赖
Inkscape 或工程文件。

输入文件必须是背景透明、没有外部资源并声明有效 `viewBox` 的独立 SVG。工具保持图标宽高比，
将整个 `viewBox` 居中适配到按钮安全区域。需要校正视觉重量或光学中心时可以调整缩放与偏移：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- compose-game-button \
  floating path/to/Icon.svg NewButton \
  --scale 1.1 --offset-x 0.5 --offset-y -1
```

还支持 `--asset-directory <directory>` 指定输出目录。命令默认拒绝覆盖普通态或按下态中的任意
现有文件；确认需要成对替换时使用 `--overwrite`。生成后应重新生成图集，并在最终尺寸下检查
图标的视觉大小、居中和线条质量。

该命令有意只接受 SVG 图标并输出 SVG 按钮。PNG 图标继续作为独立的像素资产管理；不要为了
套用游戏内按钮边框而自动重采样 PNG。确实需要 PNG 按钮时，应先根据目标尺寸和 Alpha 来源
评估其像素质量，再决定是否增加单独的光栅合成流程。
