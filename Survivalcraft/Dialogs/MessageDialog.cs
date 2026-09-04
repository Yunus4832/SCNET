using System.Xml.Linq;

namespace Game.Dialogs;

public class MessageDialog : Dialog
{
    private readonly ButtonWidget _button1Widget;

    private readonly ButtonWidget _button2Widget;

    private readonly Action<MessageDialogButton> _handler;

    private readonly LabelWidget _largeLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Vector2 size,
        Action<MessageDialogButton> handler
    )
    {
        _handler = handler;
        var node = ContentManager.Get<XElement>("Dialogs/MessageDialog");
        LoadContents(this, node);
        Size = new Vector2(size.X >= 0f ? size.X : Size.X, size.Y >= 0f ? size.Y : Size.Y);
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
    ) : this(largeMessage, smallMessage, button1Text, string.Empty, delegate { })
    {
    }

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Action<MessageDialogButton> handler
    ) : this(largeMessage, smallMessage, button1Text, button2Text, new Vector2(-1f), handler)
    {
    }

    public MessageDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Vector2 size,
        Action<MessageDialogButton, MessageDialog> selfContainedHandler
    ) : this(largeMessage, smallMessage, button1Text, button2Text, size, _ => { })
    {
        _handler = delegate (MessageDialogButton button) { selfContainedHandler(button, this); };
    }

    public bool AutoHide { get; set; }

    public override void Update()
    {
        if (Input.Cancel)
        {
            Dismiss(_button2Widget.IsVisible ? MessageDialogButton.Button2 : MessageDialogButton.Button1);
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
}
