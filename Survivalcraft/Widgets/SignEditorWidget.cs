using System.Xml.Linq;

namespace Game.Widgets;

public sealed class SignEditorWidget : CanvasWidget, IGameInputCapturingWidget
{
    private readonly Color[] _availableColors =
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

    private readonly ButtonWidget _cancelButton;
    private readonly Action _close;
    private readonly ButtonWidget[] _colorButtons = new ButtonWidget[4];
    private readonly Action<SignData> _save;
    private readonly ButtonWidget _saveButton;
    private readonly TextBoxWidget[] _textBoxes = new TextBoxWidget[4];
    private readonly TextBoxWidget _urlTextBox;
    private bool _hasChanges;

    public SignEditorWidget(SignData? signData, Action<SignData> save, Action close)
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/SignEditorWidget"));
        _save = save;
        _close = close;
        _saveButton = Children.Find<ButtonWidget>("SignEditor.Save")!;
        _cancelButton = Children.Find<ButtonWidget>("SignEditor.Cancel")!;
        _urlTextBox = Children.Find<TextBoxWidget>("SignEditor.Url")!;

        for (var i = 0; i < 4; i++)
        {
            _textBoxes[i] = Children.Find<TextBoxWidget>($"SignEditor.Text{i + 1}")!;
            _colorButtons[i] = Children.Find<ButtonWidget>($"SignEditor.Color{i + 1}")!;
            _textBoxes[i].Text = signData?.Lines.ElementAtOrDefault(i) ?? string.Empty;
            _colorButtons[i].Color = signData?.Colors.ElementAtOrDefault(i) ?? Color.Black;
            _textBoxes[i].TextChanged += _ => _hasChanges = true;
        }

        _urlTextBox.Text = signData?.Url ?? string.Empty;
        _urlTextBox.TextChanged += _ => _hasChanges = true;
    }

    public override void Update()
    {
        _saveButton.IsEnabled = _hasChanges;
        for (var i = 0; i < _colorButtons.Length; i++)
        {
            if (!_colorButtons[i].IsClicked)
            {
                continue;
            }

            var colorIndex = _availableColors.FirstIndex(_colorButtons[i].Color);
            _colorButtons[i].Color = _availableColors[(colorIndex + 1) % _availableColors.Length];
            _hasChanges = true;
        }

        if (_saveButton.IsClicked)
        {
            _save(new SignData
            {
                Lines = _textBoxes.Select(textBox => textBox.Text).ToArray(),
                Colors = _colorButtons.Select(button => button.Color).ToArray(),
                Url = _urlTextBox.Text.Trim()
            });
            _close();
        }

        if (_cancelButton.IsClicked)
        {
            _close();
        }
    }

}
