using System.Globalization;
using System.Xml.Linq;

namespace Game.Dialogs;

public class LevelFactorDialog : Dialog
{
    private readonly LabelWidget _descriptionWidget;

    private readonly LabelWidget _namesWidget;

    private readonly ButtonWidget _okWidget;

    private readonly LabelWidget _titleWidget;

    private readonly LabelWidget _totalNameWidget;

    private readonly LabelWidget _totalValueWidget;

    private readonly LabelWidget _valuesWidget;

    public LevelFactorDialog(string title, string description, IEnumerable<ComponentLevel.Factor> factors, float total)
    {
        var node = ContentManager.Get<XElement>("Dialogs/LevelFactorDialog");
        LoadContents(this, node);
        _titleWidget = Children.Find<LabelWidget>("LevelFactorDialog.Title")!;
        _descriptionWidget = Children.Find<LabelWidget>("LevelFactorDialog.Description")!;
        _namesWidget = Children.Find<LabelWidget>("LevelFactorDialog.Names")!;
        _valuesWidget = Children.Find<LabelWidget>("LevelFactorDialog.Values")!;
        _totalNameWidget = Children.Find<LabelWidget>("LevelFactorDialog.TotalName")!;
        _totalValueWidget = Children.Find<LabelWidget>("LevelFactorDialog.TotalValue")!;
        _okWidget = Children.Find<ButtonWidget>("LevelFactorDialog.OK")!;
        _titleWidget.Text = title;
        _descriptionWidget.Text = description;
        _namesWidget.Text = string.Empty;
        _valuesWidget.Text = string.Empty;
        foreach (var factor in factors)
        {
            _namesWidget.Text += $"{factor.Description,24}\n";
            _valuesWidget.Text += string.Format(CultureInfo.InvariantCulture, "x {0:0.00}\n", factor.Value);
        }

        _namesWidget.Text = _namesWidget.Text.TrimEnd();
        _valuesWidget.Text = _valuesWidget.Text.TrimEnd();
        _totalNameWidget.Text = $"{"TOTAL",24}";
        _totalValueWidget.Text = string.Format(CultureInfo.InvariantCulture, "x {0:0.00}", total);
    }

    public override void Update()
    {
        if (Input.Cancel || _okWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
