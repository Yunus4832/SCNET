using System.Xml.Linq;

namespace Game.Dialogs;

public class MoreCommunityLinkDialog : Dialog
{
    private readonly ButtonWidget _changeUserButton;

    private readonly ButtonWidget _closeButton;

    private readonly ButtonWidget _copyUserIdButton;

    private readonly ButtonWidget _publishButton;

    private readonly LabelWidget _userIdLabel;

    private readonly LabelWidget _userLabel;

    public MoreCommunityLinkDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/MoreCommunityLinkDialog");
        LoadContents(this, node);
        _userLabel = Children.Find<LabelWidget>("MoreCommunityLinkDialog.User")!;
        _changeUserButton = Children.Find<ButtonWidget>("MoreCommunityLinkDialog.ChangeUser")!;
        _userIdLabel = Children.Find<LabelWidget>("MoreCommunityLinkDialog.UserId")!;
        _copyUserIdButton = Children.Find<ButtonWidget>("MoreCommunityLinkDialog.CopyUserId")!;
        _publishButton = Children.Find<ButtonWidget>("MoreCommunityLinkDialog.Publish")!;
        _closeButton = Children.Find<ButtonWidget>("MoreCommunityLinkDialog.Close")!;
    }

    public override void Update()
    {
        var text = UserManager.ActiveUser != null ? UserManager.ActiveUser.DisplayName : "No User";
        if (text.Length > 15)
        {
            text = text[..15] + "...";
        }

        _userLabel.Text = text;
        var text2 = UserManager.ActiveUser != null ? UserManager.ActiveUser.UniqueId : "No User";
        if (text2.Length > 15)
        {
            text2 = text2[..15] + "...";
        }

        _userIdLabel.Text = text2;
        _publishButton.IsEnabled = UserManager.ActiveUser != null;
        _copyUserIdButton.IsEnabled = UserManager.ActiveUser != null;
        if (_changeUserButton.IsClicked)
        {
            DialogsManager.ShowDialog(
                ParentWidget,
                new ListSelectionDialog(
                    "Select Active User",
                    UserManager.GetUsers(),
                    60f,
                    item => ((UserInfo)item).DisplayName,
                    delegate(object item) { UserManager.ActiveUser = (UserInfo)item; }
                )
            );
        }

        if (_copyUserIdButton.IsClicked && UserManager.ActiveUser != null)
        {
            ClipboardManager.ClipboardString = UserManager.ActiveUser.UniqueId;
        }

        if (_publishButton.IsClicked && UserManager.ActiveUser != null)
        {
            DialogsManager.ShowDialog(
                ParentWidget,
                new PublishCommunityLinkDialog(
                    UserManager.ActiveUser.UniqueId,
                    string.Empty,
                    string.Empty
                )
            );
        }

        if (Input.Cancel || _closeButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
