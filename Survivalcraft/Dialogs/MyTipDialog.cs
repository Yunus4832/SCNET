namespace Game.Dialogs;

public class MyTipDialog : Dialog
{
    private readonly BevelledButtonWidget _bevelledButtonWidget = new();

    private readonly CanvasWidget _canvasWidget = new();

    private readonly LabelWidget _labelWidget = new();

    public MyTipDialog(string text, string cancel)
    {
        Children.Add(_canvasWidget);
        var stackPanel = new StackPanelWidget
        {
            HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center,
            Direction = LayoutDirection.Vertical
        };
        _bevelledButtonWidget.Text = cancel;
        _labelWidget.Text = text;
        stackPanel.Children.Add(_labelWidget);
        _canvasWidget.Children.Add(stackPanel);
    }

    public override void Update()
    {
        if (_bevelledButtonWidget.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }
}
