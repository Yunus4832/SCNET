# UI 预览工具

`UiPreviewTool` 使用游戏现有的控件、字体、图集和图形后端，构造最小图形运行环境，
不启动游戏主循环、不创建玩家或加载世界，也不读取或修改游戏实例配置。
工具版本独立维护在 `SharedProperties/Versions.props` 的 `UiPreviewToolVersion`。
游戏、引擎及模组的版本不随工具更新。

## 运行

先按 [构建文档](../Doc/BuildAndConfig.md)准备本地 NuGet 依赖。工具需要 .NET 10 和可用的桌面
图形环境，会短暂打开一个小窗口；这是离屏渲染输出，不是无图形上下文的软件截图。
当前使用 Engine 支持的 Linux x64、Windows x64 桌面后端；Linux 已实际验证，Windows 需在对应环境验证。

从仓库根目录运行：

```sh
dotnet run --project UiPreviewTool -- \
  --xml UiPreviewTool/Samples/Form.xml \
  --culture zh-CN,en-US,pt-PT,ru-RU \
  --size 960x640 --scale 1 \
  --output .agent-work/ui-preview/form-01 --strict
```

PowerShell 使用同样的参数，命令可写在一行。

- `--xml FILE`：直接构造一个自包含 Widget XML，支持现有资源和样式引用。
- `--assembly FILE --factory TYPE`：从外部场景程序集构造带测试数据的组件，不能与 `--xml` 混用。
- `--output DIRECTORY`：必须显式指定，目录须不存在或为空，避免覆盖上一轮证据。
- `--culture`：逗号分隔的内置语言标识，默认 `zh-CN`；不支持的语言会报错。
- `--size`：输出像素尺寸，默认 `960x640`，每边 64–8192；实际 GPU 上限仍以运行环境为准。
- `--scale`：逻辑 UI 到输出像素的倍率，默认 1，范围 0.25–4。
- `--strict`：布局警告也使用非零退出码。
- `--help`、`--version`：不创建图形窗口。

工具将被测根组件居中于预览视口；视口的逻辑尺寸为输出像素尺寸除以 scale。
每种语言重新创建组件，布局执行六轮，以覆盖滚动列表在取得视口尺寸后才创建行控件的过程。
不调用组件的 `Update()`，不注入输入、不发网络请求；动态状态由场景工厂在构造时准备。
需要依赖完整世界状态的 Screen/Widget 不能直接用 XML 独立加载，应准备明确的场景或继续在游戏中测试。

## 输出与判定

每种语言输出 `<culture>.png` 和 `<culture>.xml`，总索引为 `preview.xml`。
XML 保存控件类型、名称、路径、文本、实际尺寸、屏幕边界、可见/禁用状态、裁切范围，
以及 Label 的换行、截断和字号。坐标为实际输出像素，尺寸另记录控件的逻辑大小。

自动检查刻意保持有限：可见控件的非有限边界属于错误，被测根组件超出视口或被视口强制压缩属于警告。
滚动列表中的正常裁切只记录 `Clipped`，不会误判为错误。
工具不把所有控件重叠都判为错误，也不把有意的文本省略判为失败；字号是否合适、内容是否协调、
文字是否被意外遮挡仍需要结合截图与布局树审查。

退出码：0 成功；1 图形初始化、资源或场景运行失败；2 参数或场景加载错误；3 布局检查失败。
运行失败时保留已生成的图像与报告；不要把单纯生成 PNG 当作场景正确的证明。

## 外部 C# 场景

工具不直接依赖领地石或其他业务模组。场景类放在独立测试程序集，可引用工具项目或编译输出，
并实现公开接口。下面的场景只使用现有对话框，不依赖世界：

```csharp
using Game.Dialogs;
using Game.Managers;
using Game.Widgets;
using Scnet.UiPreviewTool;

public sealed class ConfirmationPreview : IUiPreviewScenario
{
    public Widget Create(UiPreviewContext context) => new MessageDialog(
        "Preview", context.Culture,
        LanguageManager.Get("Usual", "yes"),
        LanguageManager.Get("Usual", "no"), _ => { });
}
```

场景程序集项目可以使用：

```xml
<ProjectReference Include="../UiPreviewTool/UiPreviewTool.csproj"/>
```

编译后执行：

```sh
dotnet run --project UiPreviewTool -- \
  --assembly /path/to/PreviewScenes.dll --factory ConfirmationPreview \
  --culture zh-CN,en-US --output .agent-work/ui-preview/confirmation-01
```

`CreateMods()` 默认返回空集合；预览模组组件时可返回所需的 `ModDescriptor`。
场景构造函数应保持轻量，不创建控件或图形资源；这些工作放在 `Create` 中。
工具在创建组件前安装资源、初始化对应语言和图集。`Create` 得到语言和可用逻辑尺寸，
应创建全新的组件与测试数据。场景可以实现 `IDisposable` 清理自己拥有的资源；组件由工具释放。
场景代码是受信任的本地代码，工具不是沙箱，也不会自动替有复杂生命周期的组件伪造游戏世界。

常用场景包括空列表、多条记录、长名称、禁用操作、不同语言和紧凑视口。需要比较不同状态时，
使用不同工厂类型或 XML，并分别选择新的输出目录；目前不内置业务场景列表。

## 最小运行环境验证方法

UI 和着色器验证共享一种工程方法，但不要求共用一个万能工具：

1. 确定要验证的行为，选择足以覆盖它的最小真实运行环境。
2. 使用生产组件、资源与渲染路径，明确哪些数据由测试构造。
3. 准备可重复的输入和状态，并先确认场景确实覆盖了问题。
4. 同时保存可查看的产物和可量化断言，不只检查程序是否存活。
5. 说明证据边界，只有涉及真实世界生命周期、网络或平台行为时才进入完整游戏验证。

本工具把该方法用于 UI：截图配合布局树与坐标检查。着色器则应构造几何体、相机和深度缓冲，
通过像素读回检查透明混合、共面绘制、前景遮挡和不同角度。着色器成功编译不代表遮挡行为正确；
UI 截图排版正确也不代表焦点、触摸、按钮回调和网络回执正确。

目前工具仅用于开发，不新增应用发行或 NuGet 包条目。配套的 [scnet-ui-design Skill](../.agents/skills/scnet-ui-design/SKILL.md)
负责场景准备、语言/尺寸选择、产物审查及完整游戏验证的取舍，初始化和渲染由工具统一处理。
