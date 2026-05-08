using System.Xml.Linq;
using Engine.Media;

namespace Game.Widgets;

public class SliderWidget : CanvasWidget
{
    private readonly CanvasWidget _canvasWidget;

    private Vector2? _dragStartPoint;

    private float _granularity = 0.1f;

    private readonly CanvasWidget _labelCanvasWidget;

    private readonly LabelWidget _labelWidget;

    private readonly Widget _tabWidget;

    public Color TextColor
    {
        get => _labelWidget.Color;
        set => _labelWidget.Color = value;
    }

    public bool IsSliding { get; set; }

    public LayoutDirection LayoutDirection { get; set; }

    public float MinValue
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            MaxValue = MathUtils.Max(MinValue, MaxValue);
            Value = MathUtils.Clamp(Value, MinValue, MaxValue);
        }
    }

    public float MaxValue
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            MinValue = MathUtils.Min(MinValue, MaxValue);
            Value = MathUtils.Clamp(Value, MinValue, MaxValue);
        }
    } = 1f;

    public float Value
    {
        get;
        set =>
            field = _granularity > 0f
                ? MathUtils.Round(MathUtils.Clamp(value, MinValue, MaxValue) / _granularity) * _granularity
                : MathUtils.Clamp(value, MinValue, MaxValue);
    }

    public float Granularity
    {
        get => _granularity;
        set => _granularity = MathUtils.Max(value, 0f);
    }

    public string Text
    {
        get => _labelWidget.Text;
        set => _labelWidget.Text = value;
    }

    public BitmapFont Font
    {
        get => _labelWidget.Font;
        set => _labelWidget.Font = value;
    }

    public string SoundName { get; set; } = string.Empty;

    public bool IsLabelVisible
    {
        get => _labelCanvasWidget.IsVisible;
        set => _labelCanvasWidget.IsVisible = value;
    }

    public float LabelWidth
    {
        get => _labelCanvasWidget.Size.X;
        set => _labelCanvasWidget.Size = new Vector2(value, _labelCanvasWidget.Size.Y);
    }

    public bool SlidingCompleted { get; private set; }


    public SliderWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/SliderContents");
        LoadChildren(this, node);
        _canvasWidget = Children.Find<CanvasWidget>("Slider.Canvas")!;
        _labelCanvasWidget = Children.Find<CanvasWidget>("Slider.LabelCanvas")!;
        _tabWidget = Children.Find<Widget>("Slider.Tab")!;
        _labelWidget = Children.Find<LabelWidget>("Slider.Label")!;
        LoadProperties(this, node);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        base.MeasureOverride(parentAvailableSize);
        IsDrawRequired = true;
    }

    public override void ArrangeOverride()
    {
        base.ArrangeOverride();
        var num = LayoutDirection == LayoutDirection.Horizontal
            ? _canvasWidget.ActualSize.X
            : _canvasWidget.ActualSize.Y;
        var num2 = LayoutDirection == LayoutDirection.Horizontal ? _tabWidget.ActualSize.X : _tabWidget.ActualSize.Y;
        var num3 = MaxValue > MinValue ? (Value - MinValue) / (MaxValue - MinValue) : 0f;
        if (LayoutDirection == LayoutDirection.Horizontal)
        {
            var zero = Vector2.Zero;
            zero.X = num3 * (num - num2);
            zero.Y = MathUtils.Max((ActualSize.Y - _tabWidget.ActualSize.Y) / 2f, 0f);
            _canvasWidget.SetWidgetPosition(_tabWidget, zero);
        }
        else
        {
            var zero2 = Vector2.Zero;
            zero2.X = MathUtils.Max(ActualSize.X - _tabWidget.ActualSize.X, 0f) / 2f;
            zero2.Y = num3 * (num - num2);
            _canvasWidget.SetWidgetPosition(_tabWidget, zero2);
        }

        base.ArrangeOverride();
    }

    public override void Update()
    {
        var num = LayoutDirection == LayoutDirection.Horizontal
            ? _canvasWidget.ActualSize.X
            : _canvasWidget.ActualSize.Y;
        var num2 = LayoutDirection == LayoutDirection.Horizontal ? _tabWidget.ActualSize.X : _tabWidget.ActualSize.Y;
        if (Input.Tap.HasValue && HitTestGlobal(Input.Tap.Value) == _tabWidget)
        {
            if (Input.Press != null)
            {
                _dragStartPoint = ScreenToWidget(Input.Press.Value);
            }
        }

        if (Input.Press.HasValue)
        {
            if (_dragStartPoint.HasValue)
            {
                var vector = ScreenToWidget(Input.Press.Value);
                var value = Value;
                if (LayoutDirection == LayoutDirection.Horizontal)
                {
                    var f = (vector.X - num2 / 2f) / (num - num2);
                    Value = MathUtils.Lerp(MinValue, MaxValue, f);
                }
                else
                {
                    var f2 = (vector.Y - num2 / 2f) / (num - num2);
                    Value = MathUtils.Lerp(MinValue, MaxValue, f2);
                }

                if (Value.UncloseTo(value) &&
                    _granularity > 0f &&
                    !string.IsNullOrEmpty(SoundName))
                {
                    AudioManager.PlaySound(SoundName, 1f, 0f, 0f);
                }
            }
        }
        else
        {
            _dragStartPoint = null;
        }

        var flag = _dragStartPoint.HasValue && IsEnabledGlobal && IsVisibleGlobal;
        SlidingCompleted = IsSliding && !flag;
        IsSliding = flag;
        if (_dragStartPoint.HasValue)
        {
            Input.Clear();
        }
    }
}
