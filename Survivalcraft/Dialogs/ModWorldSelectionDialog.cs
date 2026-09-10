using System.Xml.Linq;

namespace Game.Dialogs;

public class ModWorldSelectionDialog : Dialog
{
    private const string _typeName = nameof(ModWorldSelectionDialog);

    private readonly ButtonWidget _cancelButton;
    private readonly List<WorldSelection> _selections = [];
    private readonly ButtonWidget _okButton;
    private readonly Action<IReadOnlyList<WorldSelection>> _selectionHandler;

    public ModWorldSelectionDialog(
        string modName,
        IEnumerable<WorldInfo> worlds,
        Func<WorldInfo, bool> isSelected,
        Action<IReadOnlyList<WorldSelection>> selectionHandler)
    {
        _selectionHandler = selectionHandler;
        var node = ContentManager.Get<XElement>("Dialogs/ModWorldSelectionDialog");
        LoadContents(this, node);
        Children.Find<LabelWidget>("ModWorldSelectionDialog.Title")!.Text =
            LanguageManager.GetContentWidgets(_typeName, "Title");
        Children.Find<LabelWidget>("ModWorldSelectionDialog.ModName")!.Text = modName;
        var listStack = Children.Find<StackPanelWidget>("ModWorldSelectionDialog.List")!;
        _okButton = Children.Find<ButtonWidget>("ModWorldSelectionDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("ModWorldSelectionDialog.Cancel")!;

        foreach (var world in worlds)
        {
            var checkbox = new CheckboxWidget
            {
                Text = world.WorldSettings.Name,
                IsChecked = isSelected(world),
                HorizontalAlignment = WidgetAlignment.Near,
                Margin = new Vector2(8, 4)
            };
            _selections.Add(new WorldSelection(world, checkbox));
            listStack.Children.Add(checkbox);
        }

        _okButton.Text = LanguageManager.Ok;
        _cancelButton.Text = LanguageManager.Cancel;
    }

    public override void Update()
    {
        if (_okButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
            _selectionHandler(_selections);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    public sealed class WorldSelection(WorldInfo world, CheckboxWidget checkbox)
    {
        public WorldInfo World { get; } = world;

        public bool IsChecked => checkbox.IsChecked;
    }
}
