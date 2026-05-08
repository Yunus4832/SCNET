using Engine.Input;
using Engine.Media;

namespace Game.Widgets;

public class MotdWidget : CanvasWidget
{
    private readonly CanvasWidget _containerWidget;

    private int _currentLineIndex;

    private double _lastLineChangeTime;

    private readonly List<LineData> _lines = [];

    private int _tapsCount;

    public MotdWidget()
    {
        _containerWidget = new CanvasWidget();
        Children.Add(_containerWidget);
        MotdManager.MessageOfTheDayUpdated += MotdManagerMessageOfTheDayUpdated;
        MotdManagerMessageOfTheDayUpdated();
    }

    public override void Update()
    {
        if (Input.Tap.HasValue)
        {
            var widget = HitTestGlobal(Input.Tap.Value);
            if (widget != null && (widget == this || widget.IsChildWidgetOf(this)))
            {
                _tapsCount++;
            }
        }

        if (_tapsCount >= 5)
        {
            _tapsCount = 0;
            MotdManager.ForceRedownload();
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }

        if (Input.IsKeyDownOnce(Key.PageUp))
        {
            GotoLine(_currentLineIndex - 1);
        }

        if (Input.IsKeyDownOnce(Key.PageDown))
        {
            GotoLine(_currentLineIndex + 1);
        }

        if (_lines.Count > 0)
        {
            _currentLineIndex %= _lines.Count;
            var realTime = Time.RealTime;
            if (_lastLineChangeTime == 0.0 || realTime - _lastLineChangeTime >= _lines[_currentLineIndex].Time)
            {
                GotoLine(_lastLineChangeTime != 0.0 ? _currentLineIndex + 1 : 0);
            }

            var num2 = (float)(realTime - _lastLineChangeTime);
            var num3 = (float)(_lastLineChangeTime + _lines[_currentLineIndex].Time - 0.33000001311302185 -
                               realTime);
            SetWidgetPosition(
                position: new Vector2(
                    !(num2 < num3)
                        ? ActualSize.X *
                          (1f - MathUtils.PowSign(MathUtils.Sin(MathUtils.Saturate(1.5f * num3) * (float)Math.PI / 2f),
                              0.33f))
                        : ActualSize.X *
                          (MathUtils.PowSign(MathUtils.Sin(MathUtils.Saturate(1.5f * num2) * (float)Math.PI / 2f),
                              0.33f) - 1f), 0f), widget: _containerWidget);
            _containerWidget.Size = ActualSize;
        }
        else
        {
            _containerWidget.Children.Clear();
        }
    }

    public void GotoLine(int index)
    {
        if (_lines.Count > 0)
        {
            _currentLineIndex = MathUtils.Max(index, 0) % _lines.Count;
            _containerWidget.Children.Clear();
            _containerWidget.Children.Add(_lines[_currentLineIndex].Widget);
            _lastLineChangeTime = Time.RealTime;
            _tapsCount = 0;
        }
    }

    public void Restart()
    {
        _currentLineIndex = 0;
        _lastLineChangeTime = 0.0;
    }

    public void MotdManagerMessageOfTheDayUpdated()
    {
        _lines.Clear();
        foreach (var line in MotdManager.MessageOfTheDay.Lines)
        {
            try
            {
                var item = ParseLine(line);
                _lines.Add(item);
            }
            catch (Exception ex)
            {
                Log.Warning(
                    $"Error loading MOTD line {MotdManager.MessageOfTheDay.Lines.IndexOf(line) + 1}. Reason: {ex.Message}");
            }
        }

        Restart();
    }

    public LineData ParseLine(MotdManager.Line line)
    {
        LineData lineData;
        if (line.Node != null)
        {
            lineData = new LineData
            {
                Time = line.Time,
                Widget = LoadWidget(null, line.Node, null)
            };
        }
        else
        {
            if (string.IsNullOrEmpty(line.Text))
            {
                throw new InvalidOperationException("Invalid MOTD line.");
            }

            var stackPanelWidget = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                HorizontalAlignment = WidgetAlignment.Center,
                VerticalAlignment = WidgetAlignment.Center
            };
            var array = line.Text.Replace("\r", "").Split(new[] { "\n" }, StringSplitOptions.None);
            foreach (var item in array)
            {
                var text = item.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var widget = new LabelWidget
                {
                    Text = text,
                    Font = ContentManager.Get<BitmapFont>("Fonts/Pericles"),
                    HorizontalAlignment = WidgetAlignment.Center,
                    VerticalAlignment = WidgetAlignment.Center,
                    DropShadow = true
                };
                stackPanelWidget.Children.Add(widget);
            }

            lineData = new LineData
            {
                Time = line.Time,
                Widget = stackPanelWidget,
            };
        }

        return lineData;
    }

    public class LineData
    {
        public float Time;

        public required Widget Widget;
    }
}
