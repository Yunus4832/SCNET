using Engine.Graphics;

using Game.Messaging;

namespace Game.Widgets;

public class ToastWidget : CanvasWidget
{
    private readonly RichTextWidget _richTextWidget = new()
    {
        Size = new Vector2(660f, -1f),
        TextAnchor = TextAnchor.HorizontalCenter,
        UseDropShadow = true,
        HorizontalAlignment = WidgetAlignment.Center,
        IsHitTestVisible = false
    };

    private bool _blinking;

    private float _duration;

    private string _message = string.Empty;

    private double _messageStartTime;

    public void DisplayMessage(string text, Color color, bool blinking)
    {
        DisplayMessage(MessageContent.Plain(text), color, blinking);
    }

    public void DisplayMessage(MessageContent content, Color color, bool blinking)
    {
        ArgumentNullException.ThrowIfNull(content);
        _message = content.PlainText;
        Children.Clear();
        _richTextWidget.Content = content;
        _richTextWidget.NormalTextColor = color;
        Children.Add(_richTextWidget);
        _messageStartTime = Time.RealTime;
        _duration = blinking ? 6f : 4f + MathUtils.Min(1f * _message.Count(c => c == '\n'), 4f);
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

            _richTextWidget.ColorTransform = new Color(1f, 1f, 1f, num);
            _richTextWidget.IsVisible = true;
            if (realTime - _messageStartTime > _duration)
            {
                _message = string.Empty;
            }
        }
        else
        {
            _richTextWidget.IsVisible = false;
        }
    }
}
