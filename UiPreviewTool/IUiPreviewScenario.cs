using Game.Modding;
using Game.Widgets;

namespace Scnet.UiPreviewTool;

/// <summary>Explicit fixture construction for widgets that cannot be created from XML alone.</summary>
public interface IUiPreviewScenario
{
    IEnumerable<ModDescriptor> CreateMods() => [];

    Widget Create(UiPreviewContext context);
}
