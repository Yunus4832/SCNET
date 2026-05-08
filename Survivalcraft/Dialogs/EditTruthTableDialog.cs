using System.Xml.Linq;

namespace Game.Dialogs;

public class EditTruthTableDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private readonly Widget _gridPanel;

    private readonly Action<bool> _handler;

    private bool _ignoreTextChanges;

    private readonly Widget _linearPanel;

    private readonly TextBoxWidget _linearTextBox;

    private readonly CheckboxWidget[] _lineCheckboxes = new CheckboxWidget[16];

    private readonly ButtonWidget _okButton;

    private readonly ButtonWidget _switchViewButton;

    private TruthTableData _tmpTruthTableData;

    private readonly TruthTableData _truthTableData;

    public EditTruthTableDialog(TruthTableData truthTableData, Action<bool> handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditTruthTableDialog");
        LoadContents(this, node);
        _linearPanel = Children.Find<Widget>("EditTruthTableDialog.LinearPanel")!;
        _gridPanel = Children.Find<Widget>("EditTruthTableDialog.GridPanel")!;
        _okButton = Children.Find<ButtonWidget>("EditTruthTableDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditTruthTableDialog.Cancel")!;
        _switchViewButton = Children.Find<ButtonWidget>("EditTruthTableDialog.SwitchViewButton")!;
        _linearTextBox = Children.Find<TextBoxWidget>("EditTruthTableDialog.LinearText")!;
        for (var i = 0; i < 16; i++)
        {
            _lineCheckboxes[i] = Children.Find<CheckboxWidget>("EditTruthTableDialog.Line" + i)!;
        }

        _handler = handler;
        _truthTableData = truthTableData;
        _tmpTruthTableData = (TruthTableData)_truthTableData.Copy();
        _linearPanel.IsVisible = false;
        _linearTextBox.TextChanged += delegate
        {
            if (_ignoreTextChanges)
            {
                return;
            }

            _tmpTruthTableData = new TruthTableData();
            _tmpTruthTableData.LoadBinaryString(_linearTextBox.Text);
        };
    }

    public override void Update()
    {
        _ignoreTextChanges = true;
        try
        {
            _linearTextBox.Text = _tmpTruthTableData.SaveBinaryString();
        }
        finally
        {
            _ignoreTextChanges = false;
        }

        for (var i = 0; i < 16; i++)
        {
            if (_lineCheckboxes[i].IsClicked)
            {
                _tmpTruthTableData.Data[i] = (byte)(_tmpTruthTableData.Data[i] == 0 ? 15 : 0);
            }

            _lineCheckboxes[i].IsChecked = _tmpTruthTableData.Data[i] > 0;
        }

        if (_linearPanel.IsVisible)
        {
            _switchViewButton.Text = "Table";
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
            _truthTableData.Data = _tmpTruthTableData.Data;
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
        _handler(result);
    }
}
