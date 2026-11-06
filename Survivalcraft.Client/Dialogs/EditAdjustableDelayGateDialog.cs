using System.Xml.Linq;

namespace Game.Dialogs;

public class EditAdjustableDelayGateDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private int _delay;

    private readonly LabelWidget _delayLabel;

    private readonly SliderWidget _delaySlider;

    private readonly Action<int> _handler;

    private readonly ButtonWidget _minusButton;

    private readonly ButtonWidget _okButton;

    private readonly ButtonWidget _plusButton;

    public EditAdjustableDelayGateDialog(int delay, Action<int> handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditAdjustableDelayGateDialog");
        LoadContents(this, node);
        _delaySlider = Children.Find<SliderWidget>("EditAdjustableDelayGateDialog.DelaySlider")!;
        _plusButton = Children.Find<ButtonWidget>("EditAdjustableDelayGateDialog.PlusButton")!;
        _minusButton = Children.Find<ButtonWidget>("EditAdjustableDelayGateDialog.MinusButton")!;
        _delayLabel = Children.Find<LabelWidget>("EditAdjustableDelayGateDialog.Label")!;
        _okButton = Children.Find<ButtonWidget>("EditAdjustableDelayGateDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditAdjustableDelayGateDialog.Cancel")!;
        _handler = handler;
        _delay = delay;
        UpdateControls();
    }

    public override void Update()
    {
        if (_delaySlider.IsSliding)
        {
            _delay = (int)_delaySlider.Value;
        }

        if (_minusButton.IsClicked)
        {
            _delay = MathUtils.Max(_delay - 1, (int)_delaySlider.MinValue);
        }

        if (_plusButton.IsClicked)
        {
            _delay = MathUtils.Min(_delay + 1, (int)_delaySlider.MaxValue);
        }

        if (_okButton.IsClicked)
        {
            Dismiss(_delay);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss(null);
        }

        UpdateControls();
    }

    public void UpdateControls()
    {
        _delaySlider.Value = _delay;
        _minusButton.IsEnabled = _delay > _delaySlider.MinValue;
        _plusButton.IsEnabled = _delay < _delaySlider.MaxValue;
        _delayLabel.Text = $"{(_delay + 1) * 0.01f:0.00} seconds";
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
