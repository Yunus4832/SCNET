using Engine.Graphics;
using Engine.Serialization;

using Game.Network;

namespace Game.Widgets;

public class RichTextWidget : CanvasWidget
{
    private static readonly IReadOnlyDictionary<string, Color> _colors = CreateColorMap();

    private readonly Dictionary<ClickableWidget, RichTextAction> _actions = [];

    private List<ParsedItem> _items = [];

    public event Action<RichTextAction>? ActionRequested;

    public string Text
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value ?? string.Empty;
            _items = SimpleTagParser.Parse(field);
            Rebuild();
        }
    } = string.Empty;

    public void Rebuild()
    {
        Children.Clear();
        _actions.Clear();
        foreach (var item in _items)
        {
            AddItem(item);
        }
    }

    public override void Update()
    {
        foreach (var (widget, action) in _actions)
        {
            if (widget.IsClicked)
            {
                ActionRequested?.Invoke(action);
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var availableWidth = MathUtils.Max(parentAvailableSize.X, 0f);
        var position = Vector2.Zero;
        var lineHeight = 0f;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            child.Measure(new Vector2(availableWidth, float.PositiveInfinity));
            var size = child.ParentDesiredSize;
            if (position.X > 0f && position.X + size.X > availableWidth)
            {
                position.X = 0f;
                position.Y += lineHeight;
                lineHeight = 0f;
            }

            position.X += size.X;
            lineHeight = MathUtils.Max(lineHeight, size.Y);
        }

        DesiredSize = new Vector2(
            availableWidth,
            position.Y + lineHeight);
    }

    public override void ArrangeOverride()
    {
        var position = Vector2.Zero;
        var lineHeight = 0f;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            var size = child.ParentDesiredSize;
            if (position.X > 0f && position.X + size.X > ActualSize.X)
            {
                position.X = 0f;
                position.Y += lineHeight;
                lineHeight = 0f;
            }

            child.Arrange(position, size);
            position.X += size.X;
            lineHeight = MathUtils.Max(lineHeight, size.Y);
        }
    }

    internal static bool TryResolveColor(string value, out Color color)
    {
        if (_colors.TryGetValue(value.Trim().ToLowerInvariant(), out color))
        {
            return true;
        }

        if (HumanReadableConverter.TryConvertFromString(typeof(Color), value, out var result) &&
            result is Color converted)
        {
            color = converted;
            return true;
        }

        color = Color.White;
        return false;
    }

    private void AddItem(ParsedItem item)
    {
        if (!item.IsTag)
        {
            AddLabel(item.Content, Color.White);
            return;
        }

        switch (item.TagName.ToLowerInvariant())
        {
            case "c":
                AddLabel(
                    item.Content,
                    TryResolveColor(item.Value, out var color) ? color : Color.White);
                break;
            case "em":
                AddLegacyEmoji(item.Content);
                break;
            case "b":
                AddBlock(item.Content);
                break;
            case "p":
                AddAction(item.Content, "*位置*", Color.SkyBlue, RichTextActionKind.Position);
                break;
            default:
                AddLabel(item.Content, Color.White);
                break;
        }
    }

    private void AddLabel(string text, Color color)
    {
        Children.Add(new LabelWidget
        {
            Text = text,
            Color = color,
            FontScale = 1f,
            WordWrap = true,
            VerticalAlignment = WidgetAlignment.Center
        });
    }

    private void AddLegacyEmoji(string content)
    {
        if (!int.TryParse(content, out var id) || id is < 10 or > 90)
        {
            AddLabel(content, Color.White);
            return;
        }

        Children.Add(new RectangleWidget
        {
            Size = new Vector2(24f),
            VerticalAlignment = WidgetAlignment.Center,
            FillColor = Color.White,
            OutlineColor = Color.Transparent,
            Subtexture = new Subtexture(
                ContentManager.Get<Texture2D>($"Textures/emojis/{id}"),
                Vector2.Zero,
                Vector2.One)
        });
    }

    private void AddBlock(string content)
    {
        if (!int.TryParse(content, out var value))
        {
            AddLabel(content, Color.White);
            return;
        }

        Children.Add(new BlockIconWidget
        {
            Size = new Vector2(24f),
            VerticalAlignment = WidgetAlignment.Center,
            Depth = 0,
            Value = Terrain.ExtractContents(value)
        });
    }

    private void AddAction(
        string value,
        string label,
        Color color,
        RichTextActionKind kind)
    {
        var container = new CanvasWidget
        {
            Size = new Vector2(82, 32),
            ClampToBounds = true
        };
        var clickable = new ClickableWidget();
        container.Children.Add(clickable);
        container.Children.Add(new LabelWidget
        {
            Text = label,
            Color = color,
            VerticalAlignment = WidgetAlignment.Center
        });
        _actions.Add(clickable, new RichTextAction(kind, value));
        Children.Add(container);
    }

    private static IReadOnlyDictionary<string, Color> CreateColorMap()
    {
        return typeof(Color)
            .GetFields()
            .Where(field => field is { IsStatic: true, FieldType: not null } &&
                            field.FieldType == typeof(Color))
            .ToDictionary(
                field => field.Name.ToLowerInvariant(),
                field => (Color)field.GetValue(null)!,
                StringComparer.OrdinalIgnoreCase);
    }
}

public enum RichTextActionKind
{
    Position
}

public sealed record RichTextAction(RichTextActionKind Kind, string Value);
