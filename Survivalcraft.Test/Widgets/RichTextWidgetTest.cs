using Engine.Core;
using Engine.Graphics;

using Game.Messaging;
using Game.Widgets;

namespace Survivalcraft.Test.Widgets;

public class RichTextWidgetTest
{
    [Fact]
    public void ContinuesAcrossStyledSegmentsBeforeWrapping()
    {
        const float prefixWidth = 2f;
        const float partialBodyWidth = 4f;
        var richText = new FixedMetricsRichTextWidget
        {
            Content = new MessageContent(
            [
                new MessageSegment("AA", MessageTextStyle.Sender),
                new MessageSegment("BBBBBBBB", MessageTextStyle.Normal)
            ])
        };

        richText.Measure(new Vector2(prefixWidth + partialBodyWidth, 100f));

        Assert.True(richText.LayoutFragments.Count >= 3);
        Assert.Equal("AA", richText.LayoutFragments[0].Text);
        Assert.Equal(0f, richText.LayoutFragments[0].Position.Y);
        Assert.Equal(prefixWidth, richText.LayoutFragments[1].Position.X, 2);
        Assert.Equal(0f, richText.LayoutFragments[1].Position.Y);
        Assert.True(richText.LayoutFragments[^1].Position.Y > 0f);
    }

    [Fact]
    public void ResolvesSemanticColorsWithoutMarkupTags()
    {
        var richText = new RichTextWidget
        {
            NormalTextColor = new Color(220, 224, 228, 180)
        };

        var normal = richText.ResolveColor(MessageTextStyle.Normal);
        var error = richText.ResolveColor(MessageTextStyle.Error);

        Assert.Equal(richText.NormalTextColor, normal);
        Assert.Equal((byte)180, error.A);
        Assert.True(error.R > error.G);
    }

    [Fact]
    public void PlainTextDoesNotInterpretLegacyTags()
    {
        var richText = new RichTextWidget
        {
            Content = MessageContent.Plain("<c=red>hello</c>")
        };

        Assert.Equal("<c=red>hello</c>", richText.Content.PlainText);
        Assert.Single(richText.Content.Segments);
        Assert.Equal(MessageTextStyle.Normal, richText.Content.Segments[0].Style);
    }

    [Fact]
    public void CentersEveryWrappedLineIndependently()
    {
        var richText = new FixedMetricsRichTextWidget
        {
            Content = MessageContent.Plain("AAA BBBBBBB"),
            TextAnchor = TextAnchor.HorizontalCenter
        };

        richText.Measure(new Vector2(6f, 100f));

        var firstLine = richText.LayoutFragments
            .Where(fragment => fragment.Position.Y.CloseTo(0f))
            .ToArray();
        var secondLine = richText.LayoutFragments
            .Where(fragment => fragment.Position.Y > 0f)
            .ToArray();
        Assert.NotEmpty(firstLine);
        Assert.NotEmpty(secondLine);
        Assert.True(firstLine[0].Position.X > 0f);
        Assert.True(secondLine[0].Position.X >= 0f);
    }

    private sealed class FixedMetricsRichTextWidget : RichTextWidget
    {
        protected override int FitText(float width, string text, int start, int length) =>
            MathUtils.Min((int)MathUtils.Floor(width), length);

        protected override Vector2 MeasureText(string text) => new(text.Length, 1f);

        protected override float CalculateLineHeight() => 1f;
    }
}
