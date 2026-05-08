using System.Xml.Linq;

namespace Game.Dialogs;

public class UnSeasonSpawnDialog : Dialog
{
    private readonly LabelWidget _largeLabelWidget;

    private readonly ValueBarWidget _progressWidget;

    private readonly LabelWidget _smallLabelWidget;

    public UnSeasonSpawnDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/UnSeasonSpawnDialog");
        LoadContents(this, node);
        _largeLabelWidget = Children.Find<LabelWidget>("UnSeasonSpawnDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("UnSeasonSpawnDialog.SmallLabel")!;
        _progressWidget = Children.Find<ValueBarWidget>("UnSeasonSpawnDialog.Progress")!;
    }

    public string LargeMessage
    {
        get => _largeLabelWidget.Text;
        set => _largeLabelWidget.Text = value;
    }

    public string SmallMessage
    {
        get => _smallLabelWidget.Text;
        set => _smallLabelWidget.Text = value;
    }

    public float Progress
    {
        get => _progressWidget.Value;
        set => _progressWidget.Value = value;
    }
}
