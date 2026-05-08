namespace Game.Widgets;

public class MessageWidget : CanvasWidget
{
    private readonly AutoCanvasWidget _autoCanvasWidget;

    private bool _blinking;

    private Color _color;

    private float _duration;

    private string _message = string.Empty;

    private double _messageStartTime;

    public MessageWidget()
    {
        _autoCanvasWidget = new AutoCanvasWidget { Size = new Vector2(660, 180) };
    }

    public void DisplayMessage(string text, Color color, bool blinking)
    {
        _message = text;
        Children.Clear();
        _autoCanvasWidget.ContentText = text;
        Children.Add(_autoCanvasWidget);
        _autoCanvasWidget.HorizontalAlignment = WidgetAlignment.Center;
        _autoCanvasWidget.VerticalAlignment = WidgetAlignment.Far;
        _messageStartTime = Time.RealTime;
        _duration = blinking ? 6f : 4f + MathUtils.Min(1f * _message.Count(c => c == '\n'), 4f);
        _color = color;
        _blinking = blinking;
    }

    public override void Update()
    {
        var realTime = Time.RealTime;
        if (!string.IsNullOrEmpty(_message))
        {
            float num;
            if (_blinking)
            {
                num = MathUtils.Saturate(1f * (float)(_messageStartTime + _duration - realTime));
                if (realTime - _messageStartTime < 0.417)
                {
                    num *= MathUtils.Lerp(0.25f, 1f,
                        0.5f * (1f - MathUtils.Cos((float)Math.PI * 12f * (float)(realTime - _messageStartTime))));
                }
            }
            else
            {
                num = MathUtils.Saturate(MathUtils.Min(3f * (float)(realTime - _messageStartTime),
                    1f * (float)(_messageStartTime + _duration - realTime)));
            }

            _autoCanvasWidget.ColorTransform = _color * num;
            _autoCanvasWidget.IsVisible = true;
            if (realTime - _messageStartTime > _duration)
            {
                _message = string.Empty;
            }
        }
        else
        {
            _autoCanvasWidget.IsVisible = false;
        }
    }
}
