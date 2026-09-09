using System.Xml.Linq;

using Game.Content;

namespace Game.Dialogs;

public sealed class ContentRepositoryDialog : Dialog
{
    private readonly TextBoxWidget _addressTextBox;
    private readonly ButtonWidget _cancelButton;
    private readonly Func<string, string, bool> _handler;
    private readonly TextBoxWidget _nameTextBox;
    private readonly ButtonWidget _saveButton;

    public ContentRepositoryDialog(string title, string nameLabel, string addressLabel, string saveButtonText,
        string name, string address, Func<string, string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/ContentRepositoryDialog"));

        Children.Find<LabelWidget>("ContentRepositoryDialog.Title")!.Text = title;
        Children.Find<LabelWidget>("ContentRepositoryDialog.NameLabel")!.Text = nameLabel;
        Children.Find<LabelWidget>("ContentRepositoryDialog.AddressLabel")!.Text = addressLabel;
        _nameTextBox = Children.Find<TextBoxWidget>("ContentRepositoryDialog.Name")!;
        _addressTextBox = Children.Find<TextBoxWidget>("ContentRepositoryDialog.Address")!;
        _saveButton = Children.Find<ButtonWidget>("ContentRepositoryDialog.Save")!;
        _cancelButton = Children.Find<ButtonWidget>("ContentRepositoryDialog.Cancel")!;

        _nameTextBox.MaximumLength = 64;
        _nameTextBox.Text = name;
        _addressTextBox.MaximumLength = TemporaryContentRepositories.MaximumUrlLength;
        _addressTextBox.Text = address;
        _saveButton.Text = saveButtonText;
        _nameTextBox.HasFocus = true;
        _nameTextBox.Enter += delegate { _addressTextBox.HasFocus = true; };
        _addressTextBox.Enter += delegate { TrySave(); };
    }

    public override void Update()
    {
        if (_saveButton.IsClicked)
        {
            TrySave();
        }
        else if (Input.Cancel || _cancelButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private void TrySave()
    {
        if (_handler(_nameTextBox.Text, _addressTextBox.Text))
        {
            DialogsManager.HideDialog(this);
        }
    }
}
