using System.Xml.Linq;

namespace Game.Dialogs;

public class AlertDialog : Dialog
{
    private readonly CanvasWidget _canvasWidget = new();

    private readonly ButtonWidget _button1Widget;

    private readonly ButtonWidget _button2Widget;

    private readonly LabelWidget _largeLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    private readonly Action _okHandler;

    private readonly Action _dismissHandler;

    private readonly ScrollPanelWidget _scrollPanel;

    public AlertDialog(
        string largeMessage,
        string smallMessage,
        string button1Text,
        string button2Text,
        Action ok,
        Action dismiss
    )
    {
        _okHandler = ok;
        _dismissHandler = dismiss;
        var node = ContentManager.Get<XElement>("Dialogs/MessageDialog");
        LoadContents(this, node);
        var size = new Vector2(-1f);
        Size = new Vector2(size.X >= 0f ? size.X : Size.X, size.Y >= 0f ? size.Y : Size.Y);
        _canvasWidget.Size = new Vector2(size.X, 80f);
        _largeLabelWidget = Children.Find<LabelWidget>("MessageDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("MessageDialog.SmallLabel")!;
        _button1Widget = Children.Find<ButtonWidget>("MessageDialog.Button1")!;
        _button2Widget = Children.Find<ButtonWidget>("MessageDialog.Button2")!;
        _scrollPanel = Children.Find<ScrollPanelWidget>()!;
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

        _smallLabelWidget.HorizontalAlignment = WidgetAlignment.Center;
        _scrollPanel.Children.Clear();
        _scrollPanel.Children.Add(_canvasWidget);
        SetContent(smallMessage);
        AutoHide = true;
    }

    public bool AutoHide { get; set; }

    public void SetContent(Widget widget)
    {
        _canvasWidget.Children.Clear();
        _canvasWidget.Children.Add(_smallLabelWidget);
        _canvasWidget.Children.Add(widget);
    }

    public void SetContent(string text)
    {
        _canvasWidget.Children.Clear();
        _smallLabelWidget.Text = text;
        _canvasWidget.Children.Add(_smallLabelWidget);
    }

    public override void Update()
    {
        if (Input.Cancel)
        {
            Dismiss();
        }
        else if (Input.Ok || _button1Widget.IsClicked)
        {
            Ok();
        }
        else if (_button2Widget.IsClicked)
        {
            Dismiss();
        }
    }

    public void Ok()
    {
        _okHandler.Invoke();
    }

    public void Dismiss()
    {
        if (AutoHide)
        {
            DialogsManager.HideDialog(this);
        }

        _dismissHandler.Invoke();
    }
}
