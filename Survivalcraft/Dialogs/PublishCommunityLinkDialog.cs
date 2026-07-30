using System.Xml.Linq;

namespace Game.Dialogs;

public class PublishCommunityLinkDialog : Dialog
{
    private readonly ButtonWidget _cancelButtonWidget;

    private readonly ButtonWidget _changeTypeButtonWidget;

    private readonly TextBoxWidget _linkTextBoxWidget;

    private readonly TextBoxWidget _nameTextBoxWidget;

    private readonly ButtonWidget _publishButtonWidget;

    private ExternalContentType _type = ExternalContentType.BlocksTexture;

    private readonly RectangleWidget _typeIconWidget;

    private readonly LabelWidget _typeLabelWidget;

    private readonly string _user;

    public PublishCommunityLinkDialog(string user, string address, string name)
    {
        var node = ContentManager.Get<XElement>("Dialogs/PublishCommunityLinkDialog");
        LoadContents(this, node);
        _linkTextBoxWidget = Children.Find<TextBoxWidget>("PublishCommunityLinkDialog.Link")!;
        _nameTextBoxWidget = Children.Find<TextBoxWidget>("PublishCommunityLinkDialog.Name")!;
        _typeIconWidget = Children.Find<RectangleWidget>("PublishCommunityLinkDialog.TypeIcon")!;
        _typeLabelWidget = Children.Find<LabelWidget>("PublishCommunityLinkDialog.Type")!;
        _changeTypeButtonWidget = Children.Find<ButtonWidget>("PublishCommunityLinkDialog.ChangeType")!;
        _publishButtonWidget = Children.Find<ButtonWidget>("PublishCommunityLinkDialog.Publish")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("PublishCommunityLinkDialog.Cancel")!;
        _linkTextBoxWidget.TextChanged += delegate
        {
            _nameTextBoxWidget.Text =
                Storage.GetFileNameWithoutExtension(GetFilenameFromLink(_linkTextBoxWidget.Text));
        };
        if (!string.IsNullOrEmpty(address))
        {
            _linkTextBoxWidget.Text = address;
        }

        if (!string.IsNullOrEmpty(name))
        {
            _nameTextBoxWidget.Text = name;
        }

        _user = user;
    }

    public override void Update()
    {
        var text = _linkTextBoxWidget.Text.Trim();
        var text2 = _nameTextBoxWidget.Text.Trim();
        _typeLabelWidget.Text = ExternalContentManager.GetEntryTypeDescription(_type);
        _typeIconWidget.Subtexture = ExternalContentManager.GetEntryTypeIcon(_type);
        _publishButtonWidget.IsEnabled = text.Length > 0 && text2.Length > 0;
        if (_changeTypeButtonWidget.IsClicked)
        {
            DialogsManager.ShowDialog(ParentWidget,
                new SelectExternalContentTypeDialog("Select Content Type",
                    delegate(ExternalContentType item) { _type = item; }));
        }
        else if (Input.Cancel || _cancelButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
        else if (_publishButtonWidget.IsClicked)
        {
            var busyDialog = new CancellableBusyDialog("Publishing", false);
            DialogsManager.ShowDialog(ParentWidget, busyDialog);
            CommunityContentManager.Publish(
                text,
                text2,
                _type,
                _user,
                busyDialog.Progress,
                delegate
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.ShowDialog(ParentWidget,
                        new MessageDialog(
                            "Link Published Successfully",
                            "It should start appearing in the listings after it is moderated. Please keep the file accessible through this link, so that other community members can download it.",
                            LanguageManager.Ok,
                            string.Empty,
                            delegate { DialogsManager.HideDialog(this); }
                        )
                    );
                },
                delegate(Exception error)
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.ShowDialog(
                        ParentWidget,
                        new MessageDialog(
                            LanguageManager.Error,
                            error.Message,
                            LanguageManager.Ok
                        )
                    );
                });
        }
    }

    public static string GetFilenameFromLink(string address)
    {
        try
        {
            var text = address;
            var num = text.IndexOf('&');
            if (num > 0)
            {
                text = text.Remove(num);
            }

            var num2 = text.IndexOf('?');
            if (num2 > 0)
            {
                text = text.Remove(num2);
            }

            text = Uri.UnescapeDataString(text);
            return Storage.GetFileName(text);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
