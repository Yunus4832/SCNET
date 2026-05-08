using System.Xml.Linq;

namespace Game.Dialogs;

public class CancellableBusyDialog : Dialog
{
    private readonly bool _autoHideOnCancel;

    private readonly ButtonWidget _cancelButtonWidget;

    private readonly ButtonWidget _hideButtonWidget;

    private readonly LabelWidget _largeLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    public CancellableBusyDialog(string largeMessage, bool autoHideOnCancel, bool canHideDialog = false)
    {
        var node = ContentManager.Get<XElement>("Dialogs/CancellableBusyDialog");
        LoadContents(this, node);
        _largeLabelWidget = Children.Find<LabelWidget>("CancellableBusyDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("CancellableBusyDialog.SmallLabel")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("CancellableBusyDialog.CancelButton")!;
        _hideButtonWidget = Children.Find<ButtonWidget>("CancellableBusyDialog.HideButton")!;
        _hideButtonWidget.IsVisible = false;
        if (canHideDialog)
        {
            _hideButtonWidget.IsVisible = true;
            _cancelButtonWidget.Size = new Vector2(160, 60);
            _hideButtonWidget.Size = new Vector2(160, 60);
        }

        Progress = new CancellableProgress();
        _autoHideOnCancel = autoHideOnCancel;
        LargeMessage = largeMessage;
        ShowProgressMessage = true;
    }

    public CancellableBusyDialog(string largeMessage, string hideButtonName, bool autoHideOnCancel)
    {
        var node = ContentManager.Get<XElement>("Dialogs/CancellableBusyDialog");
        LoadContents(this, node);
        _largeLabelWidget = Children.Find<LabelWidget>("CancellableBusyDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("CancellableBusyDialog.SmallLabel")!;
        _cancelButtonWidget = Children.Find<ButtonWidget>("CancellableBusyDialog.CancelButton")!;
        _hideButtonWidget = Children.Find<ButtonWidget>("CancellableBusyDialog.HideButton")!;
        _hideButtonWidget.IsVisible = true;
        _hideButtonWidget.Text = hideButtonName;
        _cancelButtonWidget.Size = new Vector2(160, 60);
        _hideButtonWidget.Size = new Vector2(160, 60);
        Progress = new CancellableProgress();
        _autoHideOnCancel = autoHideOnCancel;
        LargeMessage = largeMessage;
        ShowProgressMessage = true;
    }

    public CancellableProgress Progress { get; set; }

    public string LargeMessage
    {
        get => _largeLabelWidget.Text;
        set
        {
            _largeLabelWidget.Text = value;
            _largeLabelWidget.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    public string SmallMessage
    {
        get => _smallLabelWidget.Text;
        set => _smallLabelWidget.Text = value;
    }

    public bool IsCancelButtonEnabled
    {
        get => _cancelButtonWidget.IsEnabled;
        set => _cancelButtonWidget.IsEnabled = value;
    }

    public bool ShowProgressMessage { get; set; }

    public override void Update()
    {
        if (ShowProgressMessage)
        {
            SmallMessage = Progress.Completed > 0f && Progress.Total > 0f
                ? $"{Progress.Completed / Progress.Total * 100f:0}%"
                : string.Empty;
        }

        if (_cancelButtonWidget.IsClicked)
        {
            Progress.Cancel();
            if (_autoHideOnCancel)
            {
                DialogsManager.HideDialog(this);
            }
        }

        if (_hideButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }

        if (Input.Cancel)
        {
            Input.Clear();
        }
    }
}
