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

        _autoHideOnCancel = autoHideOnCancel;
        LargeMessage = largeMessage;
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
        _autoHideOnCancel = autoHideOnCancel;
        LargeMessage = largeMessage;
    }

    public event Action? CancelRequested;

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

    public bool CancelOnBack { get; set; }

    public void SetProgress(long completed, long? total)
    {
        SmallMessage = total is > 0
            ? $"{Math.Clamp((double)completed / total.Value, 0d, 1d) * 100d:0}%"
            : string.Empty;
    }

    public override void Update()
    {
        if (IsCancelButtonEnabled && (_cancelButtonWidget.IsClicked || CancelOnBack && Input.Cancel))
        {
            IsCancelButtonEnabled = false;
            CancelRequested?.Invoke();
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
