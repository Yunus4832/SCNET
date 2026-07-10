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

        var panel = new CanvasWidget
        {
            Size = new Vector2(640, 460),
            ClampToBounds = true,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        panel.Children.Add(new RectangleWidget
        {
            FillColor = Color.Black,
            OutlineColor = new Color(128, 128, 128),
            OutlineThickness = 2f
        });
        Children.Add(panel);

        var stack = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(0, 12)
        };
        panel.Children.Add(stack);

        stack.Children.Add(new LabelWidget
        {
            Text = LanguageManager.GetContentWidgets(_typeName, "Title"),
            Color = Color.White,
            HorizontalAlignment = WidgetAlignment.Center,
            DropShadow = true
        });
        stack.Children.Add(new LabelWidget
        {
            Text = modName,
            Color = Color.Gray,
            HorizontalAlignment = WidgetAlignment.Center
        });
        stack.Children.Add(new CanvasWidget { Size = new Vector2(0, 12) });

        var listCanvas = new CanvasWidget
        {
            Size = new Vector2(560, 280),
            ClampToBounds = true,
            HorizontalAlignment = WidgetAlignment.Center
        };
        listCanvas.Children.Add(new RectangleWidget
        {
            FillColor = new Color(0, 0, 0, 0),
            OutlineColor = new Color(128, 128, 128),
            OutlineThickness = 1f
        });
        stack.Children.Add(listCanvas);

        var scrollPanel = new ScrollPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Margin = new Vector2(8, 6)
        };
        listCanvas.Children.Add(scrollPanel);

        var listStack = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlignment = WidgetAlignment.Stretch
        };
        scrollPanel.Children.Add(listStack);

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

        stack.Children.Add(new CanvasWidget { Size = new Vector2(0, 18) });

        var buttons = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Center
        };
        stack.Children.Add(buttons);

        _okButton = new BevelledButtonWidget
        {
            Text = LanguageManager.Ok,
            Size = new Vector2(160, 60),
            Margin = new Vector2(20, 0)
        };
        _cancelButton = new BevelledButtonWidget
        {
            Text = LanguageManager.Cancel,
            Size = new Vector2(160, 60),
            Margin = new Vector2(20, 0)
        };
        buttons.Children.Add(_okButton);
        buttons.Children.Add(_cancelButton);
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
