using System.Xml.Linq;

namespace Game.Dialogs;

public class TextBoxDialog : Dialog
{
    private readonly ButtonWidget _cancelButtonWidget;

    private readonly Action<string> _handler;

    private readonly bool _invokeHandlerOnCancel;

    private readonly ButtonWidget _okButtonWidget;

    private readonly TextBoxWidget _textBoxWidget;

    public TextBoxDialog(
        string title,
        string text,
        int maximumLength,
        Action<string> handler,
        bool invokeHandlerOnCancel = true
    )
    {
        _handler = handler;
        _invokeHandlerOnCancel = invokeHandlerOnCancel;
        var node = ContentManager.Get<XElement>("Dialogs/TextBoxDialog");
        LoadContents(this, node);
        var titleWidget = Children.Find<LabelWidget>("TextBoxDialog.Title")!;
        _textBoxWidget = Children.Find<TextBoxWidget>("TextBoxDialog.TextBox")!;
        _okButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.OkButton")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.CancelButton")!;
        titleWidget.IsVisible = !string.IsNullOrEmpty(title);
        titleWidget.Text = title;
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
        Action<TextBoxWidget> handler2,
        bool invokeHandlerOnCancel = true
    )
    {
        _handler = handler;
        var handler3 = handler2;
        _invokeHandlerOnCancel = invokeHandlerOnCancel;
        var node = ContentManager.Get<XElement>("Dialogs/TextBoxDialog");
        LoadContents(this, node);
        var titleWidget = Children.Find<LabelWidget>("TextBoxDialog.Title")!;
        _textBoxWidget = Children.Find<TextBoxWidget>("TextBoxDialog.TextBox")!;
        _okButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.OkButton")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("TextBoxDialog.CancelButton")!;
        titleWidget.IsVisible = !string.IsNullOrEmpty(title);
        titleWidget.Text = title;
        _textBoxWidget.MaximumLength = maximumLength;
        _textBoxWidget.Text = text;
        _textBoxWidget.HasFocus = true;
        _textBoxWidget.Enter += delegate { Dismiss(_textBoxWidget.Text); };
        _textBoxWidget.TextChanged += handler3.Invoke;
        AutoHide = true;
    }

    public bool AutoHide { get; set; }

    public override void Update()
    {
        if (Input.Cancel)
        {
            Dismiss(string.Empty, _invokeHandlerOnCancel);
        }
        else if (Input.Ok || _okButtonWidget.IsClicked)
        {
            Dismiss(_textBoxWidget.Text);
        }
        else if (_cancelButtonWidget.IsClicked)
        {
            Dismiss(string.Empty, _invokeHandlerOnCancel);
        }
    }

    public void Dismiss(string result, bool invokeHandler = true)
    {
        if (AutoHide)
        {
            DialogsManager.HideDialog(this);
        }

        if (invokeHandler)
        {
            _handler.Invoke(result);
        }
    }
}
