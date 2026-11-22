using Game.Modding;
using Game.Widgets;

namespace Scnet.UiPreviewTool;

/// <summary>为无法仅靠 XML 创建的控件显式构造测试夹具。</summary>
public interface IUiPreviewScenario
{
    IEnumerable<ModDescriptor> CreateMods() => [];

    Widget Create(UiPreviewContext context);
}
