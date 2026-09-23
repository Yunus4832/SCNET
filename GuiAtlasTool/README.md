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

SVG 适合按钮、摇杆等较大的可缩放图形；PNG 适合需要保持人工像素优化效果的小图标。
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
