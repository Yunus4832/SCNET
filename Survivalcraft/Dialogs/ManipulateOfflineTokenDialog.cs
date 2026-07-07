using System.Xml.Linq;

namespace Game.Dialogs;

public class ManipulateOfflineTokenDialog : Dialog
{
    private readonly ButtonWidget _cancelButtonWidget;

    private readonly ButtonWidget _copyButtonWidget;

    private readonly ButtonWidget _createButtonWidget;

    private readonly ButtonWidget _pasteButtonWidget;

    private readonly ButtonWidget _saveButtonWidget;

    private readonly TextBoxWidget _tokenTextBoxWidget;

    public ManipulateOfflineTokenDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/ManipulateOfflineTokenDialog");
        LoadContents(this, node);
        _tokenTextBoxWidget = Children.Find<TextBoxWidget>("ManipulateOfflineTokenDialog.Token")!;
        _createButtonWidget = Children.Find<ButtonWidget>("ManipulateOfflineTokenDialog.Create")!;
        _copyButtonWidget = Children.Find<ButtonWidget>("ManipulateOfflineTokenDialog.Copy")!;
        _pasteButtonWidget = Children.Find<ButtonWidget>("ManipulateOfflineTokenDialog.Paste")!;
        _saveButtonWidget = Children.Find<ButtonWidget>("ManipulateOfflineTokenDialog.Save")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("ManipulateOfflineTokenDialog.Cancel")!;
        _tokenTextBoxWidget.Text = SettingsManager.Current.OnlineAccessToken;
    }

    public override void Update()
    {
        var text = _tokenTextBoxWidget.Text;
        var clipboardString = ClipboardManager.ClipboardString;
        var isEnabled = _saveButtonWidget.IsEnabled = IsOfflineToken(text);
        _copyButtonWidget.IsEnabled = isEnabled;
        _pasteButtonWidget.IsEnabled = IsOfflineToken(clipboardString);
        if (_createButtonWidget.IsClicked)
        {
            _tokenTextBoxWidget.Text = Guid.NewGuid().ToString();
        }

        if (_copyButtonWidget.IsClicked)
        {
            ClipboardManager.ClipboardString = text;
        }

        if (_pasteButtonWidget.IsClicked)
        {
            _tokenTextBoxWidget.Text = clipboardString;
        }

        if (_saveButtonWidget.IsClicked)
        {
            SettingsManager.SetOnlineAccessToken(text);
            DialogsManager.HideDialog(this);
        }

        if (Input.Cancel || _cancelButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private bool IsOfflineToken(string text)
    {
        if (string.IsNullOrEmpty(text)) // 先检查是否为空或 null
        {
            return false;
        }

        if (text.Length != 36 || text[8] != '-' || text[13] != '-' || text[18] != '-' || text[23] != '-')
        {
            return false;
        }

        var num = text.Count(c => "0123456789ABCDEFabcdef".Contains(c.ToString()));

        return num == 32; // 确保字符是有效的十六进制字符
    }
}
