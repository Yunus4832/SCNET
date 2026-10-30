# GUI 图集源文件

工具接受 PNG、WebP 和 SVG 源资产，统一输出无损 WebP 图集。
源位图和输出图集都使用普通直通 Alpha，资源作者无需预乘。
SVG 按声明的尺寸转为图集条目，与位图一起排版合成。

`Assets/` 中每个文件都是独立图集条目，文件名成为 `Textures/Atlas/` 后的资源名。

- 位图默认使用无损 WebP，也支持普通 PNG；不要保存预乘像素。
- SVG 不依赖外部图片、字体或工程文件，根元素使用整数像素 `width`、`height` 和对应 `viewBox`。
- 同一名称只能提供一种源格式，不能并存同名 PNG、WebP、SVG。
- 不包含编辑器背景、隐藏图层或页面底色；普通态和按下态分别保存。
- 可编辑的几何图标和 HUD 按钮继续使用 SVG；位图保留最终尺寸的像素质量。

该工具只生成 GUI 图集。需要独立加载、独立生命周期或采样参数的纹理放在 `Content/Assets/Textures/`。
生成命令和构建期增量行为见 [构建配置](../Doc/BuildAndConfig.md#gui-图集)。

## 位图转换与提取

普通 PNG/WebP 可以直接放入源目录，无需预处理。需要转换为默认的无损 WebP 时使用：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-image \
  input.png GuiAtlasTool/Assets/EntryName.webp
```

仅在一次性导入已知的历史预乘图片时使用 `--premultiplied`，工具先反预乘再输出普通 WebP。
这不是常规资产制作步骤，工具不会猜测输入的 Alpha 状态。

从当前图集中提取一个或多个条目：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- extract \
  AtlasTexture.webp Atlas.txt GuiAtlasTool/Assets Entry1 Entry2
```

提取默认输出 `.webp`。转换和提取命令拒绝覆盖已有文件；需要替换时增加 `--overwrite`。
图集生成只允许 `.webp` 输出路径。栅格输入不支持动画。
同一组输出的并发生成会串行处理，已是最新的输出会直接复用，避免多个项目同时改写图集。

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

该命令有意只接受 SVG 图标并输出 SVG 按钮。PNG/WebP 图标继续作为独立的像素资产管理；不要为了
套用游戏内按钮边框而自动重采样位图。确实需要位图按钮时，应先根据目标尺寸和 Alpha 来源
评估其像素质量，再决定是否增加单独的光栅合成流程。
