using System.Xml.Linq;

namespace Game.Dialogs;

public class MessageDialog : Dialog
{
    public enum CancelBehavior
    {
        Dismiss,
        InvokeButton1,
        InvokeButton2,
        Ignore
    }

    private readonly CanvasWidget _bodyWidget;

    private readonly ButtonWidget _button1Widget;

    private readonly ButtonWidget _button2Widget;

    private readonly CancelBehavior _cancelBehavior;

    private readonly Action<MessageDialogButton> _handler;

    private readonly LabelWidget _largeLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    private readonly Vector2 _defaultBodySize;

    private readonly Vector2 _defaultSize;

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Vector2 size,
        CancelBehavior cancelBehavior,
        Action<MessageDialogButton> handler
    )
    {
        _handler = handler;
        _cancelBehavior = cancelBehavior;
        var node = ContentManager.Get<XElement>("Dialogs/MessageDialog");
        LoadContents(this, node);
        _defaultSize = Size;
        _bodyWidget = Children.Find<CanvasWidget>("MessageDialog.Body")!;
        _defaultBodySize = _bodyWidget.Size;
        _largeLabelWidget = Children.Find<LabelWidget>("MessageDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("MessageDialog.SmallLabel")!;
        _button1Widget = Children.Find<ButtonWidget>("MessageDialog.Button1")!;
        _button2Widget = Children.Find<ButtonWidget>("MessageDialog.Button2")!;
        _largeLabelWidget.IsVisible = !string.IsNullOrEmpty(largeMessage);
        _largeLabelWidget.Text = largeMessage;
        _smallLabelWidget.IsVisible = !string.IsNullOrEmpty(smallMessage);
        _smallLabelWidget.Text = smallMessage;
        _button1Widget.IsVisible = !string.IsNullOrEmpty(button1Text);
        _button1Widget.Text = button1Text;
        _button2Widget.IsVisible = !string.IsNullOrEmpty(button2Text);
        _button2Widget.Text = button2Text;
        IsHeightAutoSized = size.Y < 0f;
        var width = size.X >= 0f ? size.X : _defaultSize.X;
        _bodyWidget.Size = new Vector2(
            MathUtils.Max(_defaultBodySize.X + width - _defaultSize.X, 0f),
            _defaultBodySize.Y);
        Size = new Vector2(width, size.Y >= 0f ? size.Y : _defaultSize.Y);
        if (!IsHeightAutoSized)
        {
            UpdateBodyHeight(Size.Y);
        }

        if (!_button1Widget.IsVisible && !_button2Widget.IsVisible)
        {
            throw new InvalidOperationException("MessageDialog must have at least one button.");
        }

        AutoHide = true;
    }

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text
    ) : this(
        largeMessage,
        smallMessage,
        button1Text,
        string.Empty,
        new Vector2(-1f),
        CancelBehavior.InvokeButton1,
        _ => { })
    {
    }

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Action<MessageDialogButton> handler
    ) : this(
        largeMessage,
        smallMessage,
        button1Text,
        button2Text,
        new Vector2(-1f),
        CancelBehavior.InvokeButton2,
        handler)
    {
    }

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Vector2 size,
        CancelBehavior cancelBehavior,
        Action<MessageDialogButton, MessageDialog> selfContainedHandler
    ) : this(largeMessage, smallMessage, button1Text, button2Text, size, cancelBehavior, _ => { })
    {
        _handler = delegate (MessageDialogButton button) { selfContainedHandler(button, this); };
    }

    public bool AutoHide { get; set; }

    public bool IsHeightAutoSized { get; set; }

    public float MaximumHeight { get; set; } = 420f;

    public float MinimumHeight { get; set; } = 240f;

    public override void Update()
    {
        if (Input.Cancel)
        {
            switch (_cancelBehavior)
            {
                case CancelBehavior.Dismiss:
                    DialogsManager.HideDialog(this);
                    break;
                case CancelBehavior.InvokeButton1:
                    Dismiss(MessageDialogButton.Button1);
                    break;
                case CancelBehavior.InvokeButton2:
                    Dismiss(MessageDialogButton.Button2);
                    break;
                case CancelBehavior.Ignore:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else if (Input.Ok || _button1Widget.IsClicked)
        {
            Dismiss(MessageDialogButton.Button1);
        }
        else if (_button2Widget.IsClicked)
        {
            Dismiss(MessageDialogButton.Button2);
        }
    }

    public void Dismiss(MessageDialogButton button)
    {
        if (AutoHide)
        {
            DialogsManager.HideDialog(this);
        }

        _handler.Invoke(button);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (IsHeightAutoSized)
        {
            UpdateAdaptiveHeight();
        }

        base.MeasureOverride(parentAvailableSize);
    }

    private void UpdateAdaptiveHeight()
    {
        var maximumHeight = MathUtils.Max(MaximumHeight, MinimumHeight);
        var desiredHeight = _defaultSize.Y;
        if (_smallLabelWidget.IsVisible)
        {
            _smallLabelWidget.Measure(new Vector2(_bodyWidget.Size.X, float.PositiveInfinity));
            desiredHeight += MathUtils.Max(_smallLabelWidget.DesiredSize.Y + 24f - _defaultBodySize.Y, 0f);
        }

        var height = MathUtils.Clamp(desiredHeight, MinimumHeight, maximumHeight);
        Size = new Vector2(Size.X, height);
        UpdateBodyHeight(height);
    }

    private void UpdateBodyHeight(float dialogHeight)
    {
        _bodyWidget.Size = new Vector2(
            _bodyWidget.Size.X,
            MathUtils.Max(_defaultBodySize.Y + dialogHeight - _defaultSize.Y, 0f));
    }
}
