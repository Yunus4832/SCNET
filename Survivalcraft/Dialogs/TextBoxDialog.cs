using System.Xml.Linq;

namespace Game.Dialogs;

public class TextBoxDialog : Dialog
{
    private readonly ButtonWidget _cancelButtonWidget;

    private readonly Action<string> _handler;

    private readonly Action<TextBoxWidget> _handler2;

    private readonly ButtonWidget _okButtonWidget;

    private readonly TextBoxWidget _textBoxWidget;

    private readonly LabelWidget _titleWidget;

    public TextBoxDialog(
        string title,
        string text,
        int maximumLength,
        Action<string> handler
    )
    {
        _handler = handler;
        _handler2 = delegate { };
        var node = ContentManager.Get<XElement>("Dialogs/TextBoxDialog");
        LoadContents(this, node);
        _titleWidget = Children.Find<LabelWidget>("TextBoxDialog.Title")!;
        _textBoxWidget = Children.Find<TextBoxWidget>("TextBoxDialog.TextBox")!;
        _okButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.OkButton")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.CancelButton")!;
        _titleWidget.IsVisible = !string.IsNullOrEmpty(title);
        _titleWidget.Text = title;
        _textBoxWidget.MaximumLength = maximumLength;
        _textBoxWidget.Text = text;
        _textBoxWidget.HasFocus = true;
        _textBoxWidget.Enter += delegate { Dismiss(_textBoxWidget.Text); };
        AutoHide = true;
    }

    public TextBoxDialog(
        string title,
        string text,
        int maximumLength,
        Action<string> handler,
        Action<TextBoxWidget> handler2
    )
    {
        _handler = handler;
        _handler2 = handler2;
        var node = ContentManager.Get<XElement>("Dialogs/TextBoxDialog");
        LoadContents(this, node);
        _titleWidget = Children.Find<LabelWidget>("TextBoxDialog.Title")!;
        _textBoxWidget = Children.Find<TextBoxWidget>("TextBoxDialog.TextBox")!;
        _okButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.OkButton")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.CancelButton")!;
        _titleWidget.IsVisible = !string.IsNullOrEmpty(title);
        _titleWidget.Text = title;
        _textBoxWidget.MaximumLength = maximumLength;
        _textBoxWidget.Text = text;
        _textBoxWidget.HasFocus = true;
        _textBoxWidget.Enter += delegate { Dismiss(_textBoxWidget.Text); };
        _textBoxWidget.TextChanged += delegate(TextBoxWidget textBox) { _handler2.Invoke(textBox); };
        AutoHide = true;
    }

    public bool AutoHide { get; set; }

    public override void Update()
    {
        if (Input.Cancel)
        {
            Dismiss(string.Empty);
        }
        else if (Input.Ok || _okButtonWidget.IsClicked)
        {
            Dismiss(_textBoxWidget.Text);
        }
        else if (_cancelButtonWidget.IsClicked)
        {
            Dismiss(string.Empty);
        }
    }

    public void Dismiss(string result)
    {
        if (AutoHide)
        {
            DialogsManager.HideDialog(this);
        }

        _handler.Invoke(result);
    }
}
