using System.Xml.Linq;

namespace Game.Dialogs;

public class RateCommunityContentDialog : Dialog
{
    private readonly string _address;

    private readonly ButtonWidget _cancelButton;

    private readonly string _displayName;

    private readonly LabelWidget _nameLabel;

    private readonly ButtonWidget _rateButton;

    private readonly LinkWidget _reportLink;

    private readonly StarRatingWidget _starRating;

    private readonly string _userId;

    public RateCommunityContentDialog(string address, string displayName, string userId)
    {
        _address = address;
        _displayName = displayName;
        _userId = userId;
        var node = ContentManager.Get<XElement>("Dialogs/RateCommunityContentDialog");
        LoadContents(this, node);
        _nameLabel = Children.Find<LabelWidget>("RateCommunityContentDialog.Name")!;
        _starRating = Children.Find<StarRatingWidget>("RateCommunityContentDialog.StarRating")!;
        _rateButton = Children.Find<ButtonWidget>("RateCommunityContentDialog.Rate")!;
        _reportLink = Children.Find<LinkWidget>("RateCommunityContentDialog.Report")!;
        _cancelButton = Children.Find<ButtonWidget>("RateCommunityContentDialog.Cancel")!;
        _nameLabel.Text = displayName;
        _rateButton.IsEnabled = false;
    }

    public override void Update()
    {
        _rateButton.IsEnabled = _starRating.Rating != 0f;
        if (_rateButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
            var busyDialog = new CancellableBusyDialog("Sending Rating", false);
            DialogsManager.ShowDialog(ParentWidget, busyDialog);
            CommunityContentManager.Rate(
                _address,
                _userId,
                (int)_starRating.Rating,
                busyDialog.Progress,
                delegate { DialogsManager.HideDialog(busyDialog); },
                delegate { DialogsManager.HideDialog(busyDialog); }
            );
        }

        if (_reportLink.IsClicked && UserManager.ActiveUser != null)
        {
            DialogsManager.HideDialog(this);
            DialogsManager.ShowDialog(
                ParentWidget,
                new ReportCommunityContentDialog(_address, _displayName, UserManager.ActiveUser.UniqueId)
            );
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
