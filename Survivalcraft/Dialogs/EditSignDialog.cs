using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Dialogs;

public class EditSignDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private readonly ButtonWidget _colorButton1;

    private readonly ButtonWidget _colorButton2;

    private readonly ButtonWidget _colorButton3;

    private readonly ButtonWidget _colorButton4;

    private readonly Color[] _colors =
    [
        new(0, 0, 0),
        new(140, 0, 0),
        new(0, 112, 0),
        new(0, 0, 96),
        new(160, 0, 128),
        new(0, 112, 112),
        new(160, 112, 0),
        new(180, 180, 180)
    ];

    private readonly ButtonWidget _linesButton;

    private readonly ContainerWidget _linesPage;

    private readonly ButtonWidget _okButton;

    private readonly Point3 _signPoint;

    private readonly SubsystemSignBlockBehavior _subsystemSignBlockBehavior;

    private readonly TextBoxWidget _textBox1;

    private readonly TextBoxWidget _textBox2;

    private readonly TextBoxWidget _textBox3;

    private readonly TextBoxWidget _textBox4;

    private readonly ButtonWidget _urlButton;

    private readonly ContainerWidget _urlPage;

    private readonly ButtonWidget _urlTestButton;

    private readonly TextBoxWidget _urlTextBox;

    public EditSignDialog(SubsystemSignBlockBehavior subsystemSignBlockBehavior, Point3 signPoint)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditSignDialog");
        LoadContents(this, node);
        _linesPage = Children.Find<ContainerWidget>("EditSignDialog.LinesPage")!;
        _urlPage = Children.Find<ContainerWidget>("EditSignDialog.UrlPage")!;
        _textBox1 = Children.Find<TextBoxWidget>("EditSignDialog.TextBox1")!;
        _textBox2 = Children.Find<TextBoxWidget>("EditSignDialog.TextBox2")!;
        _textBox3 = Children.Find<TextBoxWidget>("EditSignDialog.TextBox3")!;
        _textBox4 = Children.Find<TextBoxWidget>("EditSignDialog.TextBox4")!;
        _colorButton1 = Children.Find<ButtonWidget>("EditSignDialog.ColorButton1")!;
        _colorButton2 = Children.Find<ButtonWidget>("EditSignDialog.ColorButton2")!;
        _colorButton3 = Children.Find<ButtonWidget>("EditSignDialog.ColorButton3")!;
        _colorButton4 = Children.Find<ButtonWidget>("EditSignDialog.ColorButton4")!;
        _urlTextBox = Children.Find<TextBoxWidget>("EditSignDialog.UrlTextBox")!;
        _urlTestButton = Children.Find<ButtonWidget>("EditSignDialog.UrlTestButton")!;
        _okButton = Children.Find<ButtonWidget>("EditSignDialog.OkButton")!;
        _cancelButton = Children.Find<ButtonWidget>("EditSignDialog.CancelButton")!;
        _urlButton = Children.Find<ButtonWidget>("EditSignDialog.UrlButton")!;
        _linesButton = Children.Find<ButtonWidget>("EditSignDialog.LinesButton")!;
        _subsystemSignBlockBehavior = subsystemSignBlockBehavior;
        _signPoint = signPoint;
        var signData = _subsystemSignBlockBehavior.GetSignData(_signPoint);
        if (signData != null)
        {
            _textBox1.Text = signData.Lines[0];
            _textBox2.Text = signData.Lines[1];
            _textBox3.Text = signData.Lines[2];
            _textBox4.Text = signData.Lines[3];
            _colorButton1.Color = signData.Colors[0];
            _colorButton2.Color = signData.Colors[1];
            _colorButton3.Color = signData.Colors[2];
            _colorButton4.Color = signData.Colors[3];
            _urlTextBox.Text = signData.Url;
        }
        else
        {
            _textBox1.Text = string.Empty;
            _textBox2.Text = string.Empty;
            _textBox3.Text = string.Empty;
            _textBox4.Text = string.Empty;
            _colorButton1.Color = Color.Black;
            _colorButton2.Color = Color.Black;
            _colorButton3.Color = Color.Black;
            _colorButton4.Color = Color.Black;
            _urlTextBox.Text = string.Empty;
        }

        _linesPage.IsVisible = true;
        _urlPage.IsVisible = false;
        UpdateControls();
    }

    public override void Update()
    {
        UpdateControls();
        if (_okButton.IsClicked)
        {
            var lines = new[]
            {
                _textBox1.Text,
                _textBox2.Text,
                _textBox3.Text,
                _textBox4.Text
            };
            var colors = new[]
            {
                _colorButton1.Color,
                _colorButton2.Color,
                _colorButton3.Color,
                _colorButton4.Color
            };
            _subsystemSignBlockBehavior.SetSignData(_signPoint, lines, colors, _urlTextBox.Text);
            if (CommonLib.WorkType != WorkType.Local)
            {
                CommonLib.Net.QueuePackage(new SignBlockPackage(_signPoint, lines, colors, _urlTextBox.Text));
            }

            Dismiss();
        }

        if (_urlButton.IsClicked)
        {
            _urlPage.IsVisible = true;
            _linesPage.IsVisible = false;
        }

        if (_linesButton.IsClicked)
        {
            _urlPage.IsVisible = false;
            _linesPage.IsVisible = true;
        }

        if (_urlTestButton.IsClicked)
        {
            WebBrowserManager.LaunchBrowser(_urlTextBox.Text);
        }

        if (_colorButton1.IsClicked)
        {
            _colorButton1.Color = _colors[(_colors.FirstIndex(_colorButton1.Color) + 1) % _colors.Length];
        }

        if (_colorButton2.IsClicked)
        {
            _colorButton2.Color = _colors[(_colors.FirstIndex(_colorButton2.Color) + 1) % _colors.Length];
        }

        if (_colorButton3.IsClicked)
        {
            _colorButton3.Color = _colors[(_colors.FirstIndex(_colorButton3.Color) + 1) % _colors.Length];
        }

        if (_colorButton4.IsClicked)
        {
            _colorButton4.Color = _colors[(_colors.FirstIndex(_colorButton4.Color) + 1) % _colors.Length];
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss();
        }
    }

    public void UpdateControls()
    {
        var flag = !string.IsNullOrEmpty(_urlTextBox.Text);
        _urlButton.IsVisible = _linesPage.IsVisible;
        _linesButton.IsVisible = !_linesPage.IsVisible;
        _colorButton1.IsEnabled = !flag;
        _colorButton2.IsEnabled = !flag;
        _colorButton3.IsEnabled = !flag;
        _colorButton4.IsEnabled = !flag;
        _urlTestButton.IsEnabled = flag;
    }

    public void Dismiss()
    {
        DialogsManager.HideDialog(this);
    }
}
