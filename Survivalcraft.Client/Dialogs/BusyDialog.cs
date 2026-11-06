using System.Xml.Linq;

namespace Game.Dialogs;

public class BusyDialog : Dialog
{
    private readonly LabelWidget _largeLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    public BusyDialog(string largeMessage, string smallMessage)
    {
        var node = ContentManager.Get<XElement>("Dialogs/BusyDialog");
        LoadContents(this, node);
        _largeLabelWidget = Children.Find<LabelWidget>("BusyDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("BusyDialog.SmallLabel")!;
        LargeMessage = largeMessage;
        SmallMessage = smallMessage;
    }

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
        set
        {
            _smallLabelWidget.Text = value;
            _smallLabelWidget.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    public override void Update()
    {
        if (Input.Back)
        {
            Input.Clear();
        }
    }
}
