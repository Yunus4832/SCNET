using System.Xml.Linq;

namespace Game.Dialogs;

public class EditPistonDialog : Dialog
{
    private static readonly string[] _speedNames =
    [
        "Fast",
        "Medium",
        "Slow",
        "Very Slow"
    ];

    private readonly ButtonWidget _cancelButton;

    private readonly int _data;

    private readonly Action<int> _handler;

    private int _maxExtension;

    private readonly PistonMode _mode;

    private readonly ButtonWidget _okButton;

    private readonly ContainerWidget _panel2;

    private int _pullCount;

    private readonly SliderWidget _slider1;

    private readonly SliderWidget _slider2;

    private readonly SliderWidget _slider3;

    private int _speed;

    private readonly LabelWidget _title;

    public EditPistonDialog(int data, Action<int> handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditPistonDialog");
        LoadContents(this, node);
        _title = Children.Find<LabelWidget>("EditPistonDialog.Title")!;
        _slider1 = Children.Find<SliderWidget>("EditPistonDialog.Slider1")!;
        _panel2 = Children.Find<ContainerWidget>("EditPistonDialog.Panel2")!;
        _slider2 = Children.Find<SliderWidget>("EditPistonDialog.Slider2")!;
        _slider3 = Children.Find<SliderWidget>("EditPistonDialog.Slider3")!;
        _okButton = Children.Find<ButtonWidget>("EditPistonDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditPistonDialog.Cancel")!;
        _handler = handler;
        _data = data;
        _mode = PistonBlock.GetMode(data);
        _maxExtension = PistonBlock.GetMaxExtension(data);
        _pullCount = PistonBlock.GetPullCount(data);
        _speed = PistonBlock.GetSpeed(data);
        _title.Text = "Edit " + BlocksManager.Blocks[237].GetDisplayName(null, Terrain.MakeBlockValue(237, 0, data));
        _slider1.Granularity = 1f;
        _slider1.MinValue = 1f;
        _slider1.MaxValue = 8f;
        _slider2.Granularity = 1f;
        _slider2.MinValue = 1f;
        _slider2.MaxValue = 8f;
        _slider3.Granularity = 1f;
        _slider3.MinValue = 0f;
        _slider3.MaxValue = 3f;
        _panel2.IsVisible = _mode != PistonMode.Pushing;
        UpdateControls();
    }

    public override void Update()
    {
        if (_slider1.IsSliding)
        {
            _maxExtension = (int)_slider1.Value - 1;
        }

        if (_slider2.IsSliding)
        {
            _pullCount = (int)_slider2.Value - 1;
        }

        if (_slider3.IsSliding)
        {
            _speed = (int)_slider3.Value;
        }

        if (_okButton.IsClicked)
        {
            var value = PistonBlock.SetMaxExtension(
                PistonBlock.SetPullCount(PistonBlock.SetSpeed(_data, _speed), _pullCount), _maxExtension);
            Dismiss(value);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss(null);
        }

        UpdateControls();
    }

    public void UpdateControls()
    {
        _slider1.Value = _maxExtension + 1;
        _slider1.Text = $"{_maxExtension + 1} blocks";
        _slider2.Value = _pullCount + 1;
        _slider2.Text = $"{_pullCount + 1} blocks";
        _slider3.Value = _speed;
        _slider3.Text = _speedNames[_speed];
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
