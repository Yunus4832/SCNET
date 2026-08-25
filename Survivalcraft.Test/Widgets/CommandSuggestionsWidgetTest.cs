using System.Runtime.CompilerServices;

using Engine.Core;

using Game.Commands;
using Game.Widgets;

namespace Survivalcraft.Test.Widgets;

public class CommandSuggestionsWidgetTest
{
    [Fact]
    public void SuggestionUsesSeparatePlainTextLabelsInsteadOfMarkup()
    {
        var parts = CommandSuggestionsWidget.CreateTextParts(
            new CommandSuggestion("time", "查询或设置世界时间", false));

        Assert.Equal(2, parts.Count);
        Assert.Equal("time", parts[0].Text);
        Assert.Equal(Color.White, parts[0].Color);
        Assert.Equal("查询或设置世界时间", parts[1].Text);
        Assert.Equal(MultiplayerUiStyle.SecondaryTextColor, parts[1].Color);
        Assert.DoesNotContain(parts, part => part.Text.Contains("<c=", StringComparison.Ordinal));
    }

    [Fact]
    public void SuggestionWithoutDescriptionDoesNotAddEmptyLabel()
    {
        var parts = CommandSuggestionsWidget.CreateTextParts(
            new CommandSuggestion("list", string.Empty, false));
        var part = Assert.Single(parts);

        Assert.Equal("list", part.Text);
    }

    [Fact]
    public void LabelTextDoesNotImplicitlyUseUsualLocalization()
    {
        var label = (LabelWidget)
            RuntimeHelpers.GetUninitializedObject(typeof(LabelWidget));

        label.Text = "enable";

        Assert.Equal("enable", label.Text);
    }
}
