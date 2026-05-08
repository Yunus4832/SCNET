using System.Xml.Linq;

namespace Game.Dialogs;

public class DownloadContentFromLinkDialog : Dialog
{
    private readonly ButtonWidget _cancelButtonWidget;

    private readonly ButtonWidget _changeTypeButtonWidget;

    private readonly ButtonWidget _downloadButtonWidget;

    private readonly TextBoxWidget _linkTextBoxWidget;

    private readonly TextBoxWidget _nameTextBoxWidget;

    private ExternalContentType _type;

    private readonly RectangleWidget _typeIconWidget;

    private readonly LabelWidget _typeLabelWidget;

    private bool _updateContentName;

    private bool _updateContentType;

    public DownloadContentFromLinkDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/DownloadContentFromLinkDialog");
        LoadContents(this, node);
        _linkTextBoxWidget = Children.Find<TextBoxWidget>("DownloadContentFromLinkDialog.Link")!;
        _nameTextBoxWidget = Children.Find<TextBoxWidget>("DownloadContentFromLinkDialog.Name")!;
        _typeIconWidget = Children.Find<RectangleWidget>("DownloadContentFromLinkDialog.TypeIcon")!;
        _typeLabelWidget = Children.Find<LabelWidget>("DownloadContentFromLinkDialog.Type")!;
        _changeTypeButtonWidget = Children.Find<ButtonWidget>("DownloadContentFromLinkDialog.ChangeType")!;
        _downloadButtonWidget = Children.Find<ButtonWidget>("DownloadContentFromLinkDialog.Download")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("DownloadContentFromLinkDialog.Cancel")!;
        _linkTextBoxWidget.TextChanged += delegate
        {
            _updateContentName = true;
            _updateContentType = true;
        };
    }

    public override void Update()
    {
        var text = _linkTextBoxWidget.Text.Trim();
        var name = _nameTextBoxWidget.Text.Trim();
        _typeLabelWidget.Text = ExternalContentManager.GetEntryTypeDescription(_type);
        _typeIconWidget.Subtexture = ExternalContentManager.GetEntryTypeIcon(_type);
        if (ExternalContentManager.DoesEntryTypeRequireName(_type))
        {
            _nameTextBoxWidget.IsEnabled = true;
            _downloadButtonWidget.IsEnabled =
                text.Length > 0 && name.Length > 0 && _type != ExternalContentType.Unknown;
            if (_updateContentName)
            {
                _nameTextBoxWidget.Text = GetNameFromLink(_linkTextBoxWidget.Text);
                _updateContentName = false;
            }
        }
        else
        {
            _nameTextBoxWidget.IsEnabled = false;
            _nameTextBoxWidget.Text = string.Empty;
            _downloadButtonWidget.IsEnabled = text.Length > 0 && _type != ExternalContentType.Unknown;
        }

        if (_updateContentType)
        {
            _type = GetTypeFromLink(_linkTextBoxWidget.Text);
            _updateContentType = false;
        }

        if (_changeTypeButtonWidget.IsClicked)
        {
            DialogsManager.ShowDialog(ParentWidget, new SelectExternalContentTypeDialog("Select Content Type",
                delegate(ExternalContentType item)
                {
                    _type = item;
                    _updateContentName = true;
                }));
        }
        else if (Input.Cancel || _cancelButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
        else if (_downloadButtonWidget.IsClicked)
        {
            var busyDialog = new CancellableBusyDialog("Downloading", false);
            DialogsManager.ShowDialog(ParentWidget, busyDialog);
            WebManager.Get(
                text,
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                busyDialog.Progress,
                delegate(byte[] data)
                {
                    ExternalContentManager.ImportExternalContent(
                        new MemoryStream(data),
                        _type,
                        name,
                        delegate
                        {
                            DialogsManager.HideDialog(busyDialog);
                            DialogsManager.HideDialog(this);
                        },
                        delegate(Exception error)
                        {
                            DialogsManager.HideDialog(busyDialog);
                            DialogsManager.Alert(error.Message);
                        });
                },
                delegate(Exception error)
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(error.Message);
                }
            );
        }
    }

    public static string UnclutterLink(string address)
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

            return Uri.UnescapeDataString(text);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string GetNameFromLink(string address)
    {
        try
        {
            return Storage.GetFileNameWithoutExtension(UnclutterLink(address));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static ExternalContentType GetTypeFromLink(string address)
    {
        try
        {
            return ExternalContentManager.ExtensionToType(Storage.GetExtension(UnclutterLink(address)));
        }
        catch (Exception)
        {
            return ExternalContentType.Unknown;
        }
    }
}
