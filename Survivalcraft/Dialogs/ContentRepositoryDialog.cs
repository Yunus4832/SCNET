using System.Xml.Linq;

using Game.Content;

namespace Game.Dialogs;

public sealed class ContentRepositoryDialog : Dialog
{
    private readonly LabelWidget _namePlaceholder;

    private readonly LabelWidget _addressPlaceholder;

    private readonly TextBoxWidget _addressTextBox;

    private readonly ButtonWidget _cancelButton;

    private readonly Func<string, string, bool> _handler;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ButtonWidget _saveButton;

    public ContentRepositoryDialog(
        string title,
        string namePlaceholder,
        string addressPlaceholder,
        string saveButtonText,
        string name,
        string address,
        Func<string, string, bool> handler,
        int nameMaximumLength = 64
    )
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/ContentRepositoryDialog"));

        Children.Find<LabelWidget>("ContentRepositoryDialog.Title")!.Text = title;
        _namePlaceholder = Children.Find<LabelWidget>("ContentRepositoryDialog.NamePlaceholder")!;
        _addressPlaceholder = Children.Find<LabelWidget>("ContentRepositoryDialog.AddressPlaceholder")!;
        _namePlaceholder.Text = namePlaceholder;
        _addressPlaceholder.Text = addressPlaceholder;
        _nameTextBox = Children.Find<TextBoxWidget>("ContentRepositoryDialog.Name")!;
        _addressTextBox = Children.Find<TextBoxWidget>("ContentRepositoryDialog.Address")!;
        _saveButton = Children.Find<ButtonWidget>("ContentRepositoryDialog.Save")!;
        _cancelButton = Children.Find<ButtonWidget>("ContentRepositoryDialog.Cancel")!;

        _nameTextBox.MaximumLength = nameMaximumLength;
        _nameTextBox.Text = name;
        _addressTextBox.MaximumLength = TemporaryContentRepositories.MaximumUrlLength;
        _addressTextBox.Text = address;
        _saveButton.Text = saveButtonText;
        UpdatePlaceholders();
        _nameTextBox.HasFocus = true;
        _nameTextBox.Enter += delegate { _addressTextBox.HasFocus = true; };
        _addressTextBox.Enter += delegate { TrySave(); };
    }

    public override void Update()
    {
        UpdatePlaceholders();
        if (_saveButton.IsClicked)
        {
            TrySave();
        }
        else if (Input.Cancel || _cancelButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private void UpdatePlaceholders()
    {
        _namePlaceholder.IsVisible = !_nameTextBox.HasDisplayText;
        _addressPlaceholder.IsVisible = !_addressTextBox.HasDisplayText;
    }

    private void TrySave()
    {
        if (_handler(_nameTextBox.Text, _addressTextBox.Text))
        {
            DialogsManager.HideDialog(this);
        }
    }
}
