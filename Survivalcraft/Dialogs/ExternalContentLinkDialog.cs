using System.Xml.Linq;

namespace Game.Dialogs;

public class ExternalContentLinkDialog : Dialog
{
    private readonly ButtonWidget _okButtonWidget;

    private readonly TextBoxWidget _textBoxWidget;

    public ExternalContentLinkDialog(string link)
    {
        ClipboardManager.ClipboardString = link;
        var node = ContentManager.Get<XElement>("Dialogs/ExternalContentLinkDialog");
        LoadContents(this, node);
        _textBoxWidget = Children.Find<TextBoxWidget>("ExternalContentLinkDialog.TextBox")!;
        _okButtonWidget = Children.Find<ButtonWidget>("ExternalContentLinkDialog.OkButton")!;
        _textBoxWidget.Text = link;
    }

    public override void Update()
    {
        if (Input.Cancel || _okButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
