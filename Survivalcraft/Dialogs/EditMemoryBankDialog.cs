using System.Xml.Linq;

namespace Game.Dialogs;

public class EditMemoryBankDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private readonly Widget _gridPanel;

    private readonly Action _handler;

    private bool _ignoreTextChanges;

    private readonly Widget _linearPanel;

    private readonly TextBoxWidget _linearTextBox;

    private readonly TextBoxWidget[] _lineTextBoxes = new TextBoxWidget[16];

    private readonly MemoryBankData _memoryBankData;

    private readonly ButtonWidget _okButton;

    private readonly ButtonWidget _switchViewButton;

    private MemoryBankData _tmpMemoryBankData;

    public EditMemoryBankDialog(MemoryBankData memoryBankData, Action handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditMemoryBankDialog");
        LoadContents(this, node);
        _linearPanel = Children.Find<Widget>("EditMemoryBankDialog.LinearPanel")!;
        _gridPanel = Children.Find<Widget>("EditMemoryBankDialog.GridPanel")!;
        _okButton = Children.Find<ButtonWidget>("EditMemoryBankDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditMemoryBankDialog.Cancel")!;
        _switchViewButton = Children.Find<ButtonWidget>("EditMemoryBankDialog.SwitchViewButton")!;
        _linearTextBox = Children.Find<TextBoxWidget>("EditMemoryBankDialog.LinearText")!;
        for (var i = 0; i < 16; i++)
        {
            _lineTextBoxes[i] = Children.Find<TextBoxWidget>("EditMemoryBankDialog.Line" + i)!;
        }

        _handler = handler;
        _memoryBankData = memoryBankData;
        _tmpMemoryBankData = (MemoryBankData)_memoryBankData.Copy();
        _linearPanel.IsVisible = false;
        for (var j = 0; j < 16; j++)
        {
            _lineTextBoxes[j].TextChanged += TextBox_TextChanged;
        }

        _linearTextBox.TextChanged += TextBox_TextChanged;
    }

    public void TextBox_TextChanged(TextBoxWidget textBox)
    {
        if (_ignoreTextChanges)
        {
            return;
        }

        if (textBox == _linearTextBox)
        {
            _tmpMemoryBankData = new MemoryBankData();
            _tmpMemoryBankData.LoadString(_linearTextBox.Text);
            return;
        }

        var text = string.Empty;
        for (var i = 0; i < 16; i++)
        {
            text += _lineTextBoxes[i].Text;
        }

        _tmpMemoryBankData = new MemoryBankData();
        _tmpMemoryBankData.LoadString(text);
    }

    public override void Update()
    {
        _ignoreTextChanges = true;
        try
        {
            var text = _tmpMemoryBankData.SaveString(false);
            if (text.Length < 256)
            {
                text += new string('0', 256 - text.Length);
            }

            for (var i = 0; i < 16; i++)
            {
                _lineTextBoxes[i].Text = text.Substring(i * 16, 16);
            }

            _linearTextBox.Text = _tmpMemoryBankData.SaveString(false);
        }
        finally
        {
            _ignoreTextChanges = false;
        }

        if (_linearPanel.IsVisible)
        {
            _switchViewButton.Text = "Grid";
            if (_switchViewButton.IsClicked)
            {
                _linearPanel.IsVisible = false;
                _gridPanel.IsVisible = true;
            }
        }
        else
        {
            _switchViewButton.Text = "Linear";
            if (_switchViewButton.IsClicked)
            {
                _linearPanel.IsVisible = true;
                _gridPanel.IsVisible = false;
            }
        }

        if (_okButton.IsClicked)
        {
            _memoryBankData.Data = _tmpMemoryBankData.Data;
            Dismiss(true);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss(false);
        }
    }

    public void Dismiss(bool result)
    {
        DialogsManager.HideDialog(this);
        if (result)
        {
            _handler();
        }
    }
}
