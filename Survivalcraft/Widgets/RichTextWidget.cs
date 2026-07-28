using Engine.Graphics;
using Engine.Media;

using Game.Messaging;

namespace Game.Widgets;

internal sealed record RichTextLayoutFragment(
    string Text,
    Vector2 Position,
    Vector2 Size,
    MessageTextStyle Style);

public class RichTextWidget : Widget
{
    private readonly List<RichTextLayoutFragment> _layoutRuns = [];

    private MessageContent _content = MessageContent.Plain(string.Empty);

    private BitmapFont? _font;

    private float _layoutHeight;

    public MessageContent Content
    {
        get => _content;
        set => _content = value ?? MessageContent.Plain(string.Empty);
    }

    public BitmapFont Font
    {
        get => _font ??= ContentManager.Get<BitmapFont>("Fonts/Pericles");
        set => _font = value;
    }

    public Color NormalTextColor { get; set; } = Color.White;

    public bool UseDropShadow { get; set; }

    public bool TextureLinearFilter { get; set; } = true;

    public float ContentScale { get; set; } = 1f;

    public Vector2 FontSpacing { get; set; }

    public TextAnchor TextAnchor { get; set; }

    public Vector2 Size { get; set; } = new(-1f);

    internal IReadOnlyList<RichTextLayoutFragment> LayoutFragments => _layoutRuns;

    public override void Draw(DrawContext dc)
    {
        if (_layoutRuns.Count == 0)
        {
            return;
        }

        var samplerState = TextureLinearFilter ? SamplerState.LinearClamp : SamplerState.PointClamp;
        var fontBatch = dc.PrimitivesRenderer2D.FontBatch(
            Font,
            1,
            DepthStencilState.None,
            null,
            null,
            samplerState);
        var count = fontBatch.TriangleVertices.Count;
        var scale = new Vector2(MathUtils.Max(ContentScale, 0.1f));
        foreach (var run in _layoutRuns)
        {
            var color = ResolveColor(run.Style) * GlobalColorTransform;
            if (UseDropShadow)
            {
                fontBatch.QueueText(
                    run.Text,
                    run.Position + new Vector2(ContentScale),
                    0f,
                    new Color(0, 0, 0, (int)color.A),
                    TextAnchor.Left,
                    scale,
                    FontSpacing);
            }

            fontBatch.QueueText(
                run.Text,
                run.Position,
                0f,
                color,
                TextAnchor.Left,
                scale,
                FontSpacing);
        }

        fontBatch.TransformTriangles(GlobalTransform, count);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var availableWidth = MathUtils.Max(
            Size.X >= 0f ? MathUtils.Min(parentAvailableSize.X, Size.X) : parentAvailableSize.X,
            0f);
        BuildLayout(availableWidth);
        DesiredSize = new Vector2(
            availableWidth,
            Size.Y >= 0f ? Size.Y : _layoutHeight);
        IsDrawRequired = _layoutRuns.Count > 0;
    }

    internal Color ResolveColor(MessageTextStyle style) =>
        style switch
        {
            MessageTextStyle.Sender => NormalTextColor,
            MessageTextStyle.System => new Color(238, 154, 96, (int)NormalTextColor.A),
            MessageTextStyle.Team => new Color(104, 172, 255, (int)NormalTextColor.A),
            MessageTextStyle.Success => new Color(108, 218, 126, (int)NormalTextColor.A),
            MessageTextStyle.Error => new Color(255, 112, 112, (int)NormalTextColor.A),
            MessageTextStyle.Warning => new Color(245, 206, 96, (int)NormalTextColor.A),
            MessageTextStyle.Accent => new Color(135, 206, 235, (int)NormalTextColor.A),
            _ => NormalTextColor
        };

    private void BuildLayout(float availableWidth)
    {
        _layoutRuns.Clear();
        _layoutHeight = 0f;
        if (availableWidth <= 0f)
        {
            return;
        }

        var position = Vector2.Zero;
        var lineHeight = CalculateLineHeight();
        foreach (var segment in Content.Segments)
        {
            LayoutSegment(segment, availableWidth, lineHeight, ref position);
        }

        if (Content.Segments.Any(segment => segment.Text.Length > 0))
        {
            _layoutHeight = position.Y + lineHeight;
        }

        AlignLines(availableWidth);
    }

    private void AlignLines(float availableWidth)
    {
        if ((TextAnchor & (TextAnchor.HorizontalCenter | TextAnchor.Right)) == 0)
        {
            return;
        }

        var lineStart = 0;
        while (lineStart < _layoutRuns.Count)
        {
            var lineY = _layoutRuns[lineStart].Position.Y;
            var lineEnd = lineStart + 1;
            while (lineEnd < _layoutRuns.Count &&
                   _layoutRuns[lineEnd].Position.Y.CloseTo(lineY))
            {
                lineEnd++;
            }

            var lineWidth = 0f;
            for (var index = lineStart; index < lineEnd; index++)
            {
                var run = _layoutRuns[index];
                lineWidth = MathUtils.Max(lineWidth, run.Position.X + run.Size.X);
            }

            var offset = (TextAnchor & TextAnchor.HorizontalCenter) != 0
                ? MathUtils.Max((availableWidth - lineWidth) / 2f, 0f)
                : MathUtils.Max(availableWidth - lineWidth, 0f);
            for (var index = lineStart; index < lineEnd; index++)
            {
                var run = _layoutRuns[index];
                _layoutRuns[index] = run with
                {
                    Position = run.Position + new Vector2(offset, 0f)
                };
            }

            lineStart = lineEnd;
        }
    }

    private void LayoutSegment(
        MessageSegment segment,
        float availableWidth,
        float lineHeight,
        ref Vector2 position)
    {
        var text = segment.Text;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] is '\r')
            {
                index++;
                continue;
            }

            if (text[index] is '\n')
            {
                MoveToNextLine(lineHeight, ref position);
                index++;
                continue;
            }

            if (position.X <= 0f)
            {
                while (index < text.Length && text[index] is ' ')
                {
                    index++;
                }

                if (index >= text.Length)
                {
                    break;
                }
            }

            var newlineIndex = text.IndexOfAny(['\r', '\n'], index);
            var remainingLength = (newlineIndex >= 0 ? newlineIndex : text.Length) - index;
            var remainingWidth = MathUtils.Max(availableWidth - position.X, 0f);
            var count = FitText(
                remainingWidth,
                text,
                index,
                remainingLength);
            if (count == 0)
            {
                if (position.X > 0f)
                {
                    MoveToNextLine(lineHeight, ref position);
                    continue;
                }

                count = 1;
            }

            count = FindPreferredBreak(text, index, count, remainingLength);
            var fragment = text.Substring(index, count);
            var size = MeasureText(fragment);
            _layoutRuns.Add(new RichTextLayoutFragment(
                fragment,
                position,
                new Vector2(size.X, lineHeight),
                segment.Style));
            position.X += size.X;
            index += count;

            if (index < text.Length &&
                text[index] is not '\r' and not '\n' &&
                count < remainingLength)
            {
                MoveToNextLine(lineHeight, ref position);
            }
        }
    }

    private static int FindPreferredBreak(
        string text,
        int start,
        int fittedCount,
        int remainingLength)
    {
        if (fittedCount >= remainingLength)
        {
            return fittedCount;
        }

        for (var index = start + fittedCount - 1; index > start; index--)
        {
            if (char.IsWhiteSpace(text[index]) || char.IsPunctuation(text[index]))
            {
                return index - start + 1;
            }
        }

        return fittedCount;
    }

    protected virtual int FitText(float width, string text, int start, int length) =>
        Font.FitText(
            width,
            text,
            start,
            length,
            MathUtils.Max(ContentScale, 0.1f),
            FontSpacing.X);

    protected virtual Vector2 MeasureText(string text) =>
        Font.MeasureText(
            text,
            new Vector2(MathUtils.Max(ContentScale, 0.1f)),
            FontSpacing);

    protected virtual float CalculateLineHeight() =>
        (Font.GlyphHeight + Font.Spacing.Y + FontSpacing.Y) *
        MathUtils.Max(ContentScale, 0.1f) *
        Font.Scale;

    private static void MoveToNextLine(float lineHeight, ref Vector2 position)
    {
        position.X = 0f;
        position.Y += lineHeight;
    }
}
