using System.Xml.Linq;

namespace Game.Dialogs;

public class ReportCommunityContentDialog : Dialog
{
    private readonly string _address;

    private readonly ButtonWidget _cancelButton;

    private readonly ContainerWidget _container;

    private readonly LabelWidget _nameLabel;

    private readonly List<CheckboxWidget> _reasonWidgetsList = [];

    private readonly ButtonWidget _reportButton;

    private readonly string _userId;

    public ReportCommunityContentDialog(string address, string displayName, string userId)
    {
        _address = address;
        _userId = userId;
        var node = ContentManager.Get<XElement>("Dialogs/ReportCommunityContentDialog");
        LoadContents(this, node);
        _nameLabel = Children.Find<LabelWidget>("ReportCommunityContentDialog.Name")!;
        _container = Children.Find<ContainerWidget>("ReportCommunityContentDialog.Container")!;
        _reportButton = Children.Find<ButtonWidget>("ReportCommunityContentDialog.Report")!;
        _cancelButton = Children.Find<ButtonWidget>("ReportCommunityContentDialog.Cancel")!;
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Cruelty",
            Tag = "cruelty"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Dating",
            Tag = "dating"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Drugs / Alcohol",
            Tag = "drugs"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Hate Speech",
            Tag = "hate"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Plagiarism",
            Tag = "plagiarism"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Racism",
            Tag = "racism"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Sex / Nudity",
            Tag = "sex"
        });
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Excessive Swearing",
            Tag = "swearing"
        });
        var random = new Random();
        _reasonWidgetsList.RandomShuffle(max => random.Int(0, max - 1));
        _reasonWidgetsList.Add(new CheckboxWidget
        {
            Text = "Other",
            Tag = "other"
        });
        foreach (var reasonWidgets in _reasonWidgetsList)
        {
            _container.Children.Add(reasonWidgets);
        }

        _nameLabel.Text = displayName;
        _reportButton.IsEnabled = false;
    }

    public override void Update()
    {
        _reportButton.IsEnabled = _reasonWidgetsList.Count(w => w.IsChecked) == 1;
        if (_reportButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
            DialogsManager.ShowDialog(
                ParentWidget,
                new MessageDialog("Are you sure?",
                    "Reporting offensive content is a serious matter. Please make sure you checked the right box. Do not report content which is not offensive.",
                    "Proceed",
                    LanguageManager.Cancel,
                    delegate(MessageDialogButton b)
                    {
                        if (b != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        var report = string.Empty;
                        foreach (var reasonWidgets in _reasonWidgetsList)
                        {
                            if (!reasonWidgets.IsChecked)
                            {
                                continue;
                            }

                            report = (string)reasonWidgets.Tag;
                            break;
                        }

                        var busyDialog = new CancellableBusyDialog("Sending Report", false);
                        DialogsManager.ShowDialog(ParentWidget, busyDialog);
                        CommunityContentManager.Report(_address, _userId, report, busyDialog.Progress,
                            delegate { DialogsManager.HideDialog(busyDialog); },
                            delegate { DialogsManager.HideDialog(busyDialog); });
                    }
                )
            );
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
