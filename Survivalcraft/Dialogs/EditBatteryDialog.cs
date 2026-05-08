using System.Xml.Linq;

namespace Game.Dialogs;

public class EditBatteryDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private readonly Action<int> _handler;

    private readonly ButtonWidget _okButton;

    private int _voltageLevel;

    private readonly SliderWidget _voltageSlider;

    public EditBatteryDialog(int voltageLevel, Action<int> handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditBatteryDialog");
        LoadContents(this, node);
        _okButton = Children.Find<ButtonWidget>("EditBatteryDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditBatteryDialog.Cancel")!;
        _voltageSlider = Children.Find<SliderWidget>("EditBatteryDialog.VoltageSlider")!;
        _handler = handler;
        _voltageLevel = voltageLevel;
        UpdateControls();
    }

    public override void Update()
    {
        if (_voltageSlider.IsSliding)
        {
            _voltageLevel = (int)_voltageSlider.Value;
        }

        if (_okButton.IsClicked)
        {
            Dismiss(_voltageLevel);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss(null);
        }

        UpdateControls();
    }

    public void UpdateControls()
    {
        _voltageSlider.Text = string.Format("{0:0.0}V ({1})", 1.5f * _voltageLevel / 15f,
            _voltageLevel < 8 ? "Low" : "High");
        _voltageSlider.Value = _voltageLevel;
    }

    public void Dismiss(int? result)
    {
        DialogsManager.HideDialog(this);
        if (result.HasValue)
        {
            _handler(result.Value);
        }
    }
}
