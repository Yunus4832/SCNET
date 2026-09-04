namespace Game.Dialogs;

public class AddServerDialog : Dialog
{
    private readonly BevelledButtonWidget _add = new()
    {
        Text = "添加",
        Margin = new Vector2(180, 0),
        Size = new Vector2(120, 66),
        HorizontalAlignment = WidgetAlignment.Near
    };

    private readonly CanvasWidget _autoCanvas = new() { Margin = new Vector2(0, 10f) };

    private readonly BevelledButtonWidget _cancel = new()
    {
        Text = "取消",
        Margin = new Vector2(140, 0),
        Size = new Vector2(120, 66),
        HorizontalAlignment = WidgetAlignment.Far
    };

    private readonly CanvasWidget _canvasWidget = new()
    {
        ClampToBounds = true,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center
    };

    private TextBoxWidget _name1;

    private TextBoxWidget _ip1;

    private readonly StackPanelWidget _panelWidget = new() { Direction = LayoutDirection.Vertical };

    private readonly RectangleWidget _rectangleWidget = new()
    {
        FillColor = Color.Black,
        OutlineColor = Color.White,
        OutlineThickness = 2f
    };

    private Action<string, string> _succ;

    public AddServerDialog(Action<string, string> succd)
    {
        _succ = succd;
        _canvasWidget.Size = new Vector2(600, 240);
        _autoCanvas.Size = new Vector2(_canvasWidget.Size.X, 60);
        _canvasWidget.Children.Add(_rectangleWidget);
        _canvasWidget.Children.Add(_panelWidget);
        Children.Add(_canvasWidget);
        _autoCanvas.Children.Add(_add);
        _autoCanvas.Children.Add(_cancel);
        _panelWidget.Children.Add(EditText(new Vector2(550, 60), "服务器名称:", "name", new Vector2(0f, 10f)));
        _panelWidget.Children.Add(EditText(new Vector2(550, 60), "服务器IP:", "ip", new Vector2(0f, 10f)));
        _panelWidget.Children.Add(_autoCanvas);
        _name1 = Children.Find<TextBoxWidget>("name")!;
        _ip1 = Children.Find<TextBoxWidget>("ip")!;
    }

    private Widget EditText(Vector2 size, string title, string name, Vector2 margin)
    {
        var canvasWidget = new CanvasWidget
        { Size = size, HorizontalAlignment = WidgetAlignment.Center, Margin = margin };
        var canvasWidget2 = new CanvasWidget
        { Size = size - new Vector2(160, 0), HorizontalAlignment = WidgetAlignment.Far };
        var rectangle = new RectangleWidget { OutlineColor = Color.White };
        var label = new LabelWidget { Text = title };
        var textBoxWidget = new TextBoxWidget
        { Name = name, Size = size, HorizontalAlignment = WidgetAlignment.Center, Margin = new Vector2(3f, 0f) };
        canvasWidget2.Children.Add(rectangle);
        canvasWidget2.Children.Add(textBoxWidget);
        canvasWidget.Children.Add(label);
        canvasWidget.Children.Add(canvasWidget2);
        return canvasWidget;
    }

    public override void Update()
    {
        if (_cancel.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }

        if (!_add.IsClicked)
        {
            return;
        }

        if (string.IsNullOrEmpty(_ip1.Text) || string.IsNullOrEmpty(_name1.Text))
        {
            DialogsManager.HideAllDialogs();
            DialogsManager.Alert("请输入IP或名称");
        }
        else
        {
            DialogsManager.HideDialog(this);
            _succ.Invoke(_name1.Text, _ip1.Text);
        }
    }
}
