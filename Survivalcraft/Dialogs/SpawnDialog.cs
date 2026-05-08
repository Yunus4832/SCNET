using System.Xml.Linq;

namespace Game.Dialogs;

public class SpawnDialog : Dialog
{
    private readonly LabelWidget _largeLabelWidget;

    private readonly ValueBarWidget _progressWidget;

    private readonly LabelWidget _seasonLabelWidget;

    private readonly LabelWidget _smallLabelWidget;

    public SpawnDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/SpawnDialog");
        LoadContents(this, node);
        _seasonLabelWidget = Children.Find<LabelWidget>("SpawnDialog.SeasonLabel")!;
        _largeLabelWidget = Children.Find<LabelWidget>("SpawnDialog.LargeLabel")!;
        _smallLabelWidget = Children.Find<LabelWidget>("SpawnDialog.SmallLabel")!;
        _progressWidget = Children.Find<ValueBarWidget>("SpawnDialog.Progress")!;
    }

    public string LargeMessage
    {
        get => _largeLabelWidget.Text;
        set => _largeLabelWidget.Text = value;
    }

    public float TimeOfYear
    {
        set
        {
            _seasonLabelWidget.Text = SubsystemSeasons.GetTimeOfYearName(value);
            _seasonLabelWidget.Color = SubsystemSeasons.GetTimeOfYearColor(value);
            _progressWidget.LitBarColor = SubsystemSeasons.GetTimeOfYearColor(value);
        }
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
