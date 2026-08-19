using System.Xml.Linq;

using Game.Commands;

namespace Game.Screens;

public class InstanceManagementScreen : Screen
{
    private readonly ButtonWidget _createButton;

    private readonly ButtonWidget _deleteButton;

    private readonly ListPanelWidget _instancesList;

    private Screen? _previousScreen;

    private readonly ButtonWidget _switchButton;

    public InstanceManagementScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/InstanceManagementScreen"));
        _instancesList = Children.Find<ListPanelWidget>("InstancesList")!;
        _createButton = Children.Find<ButtonWidget>("CreateButton")!;
        _deleteButton = Children.Find<ButtonWidget>("DeleteButton")!;
        _switchButton = Children.Find<ButtonWidget>("SwitchButton")!;
        _instancesList.ItemWidgetFactory = CreateInstanceItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        _previousScreen = ScreensManager.PreviousScreen;
        RefreshInstances();
    }

    public override void Leave()
    {
        _instancesList.SelectedItem = null;
    }

    public override void Update()
    {
        var selected = _instancesList.SelectedItem as InstanceItem;
        var isCurrent = selected?.IsCurrent == true;
        _deleteButton.IsEnabled = selected != null && !isCurrent && !selected.IsRunning;
        _switchButton.IsEnabled = selected != null && !isCurrent;

        if (_createButton.IsClicked)
        {
            ShowCreateDialog();
        }

        if (_deleteButton.IsClicked && selected != null && !isCurrent)
        {
            ConfirmDelete(selected);
        }

        if (_switchButton.IsClicked && selected != null && !isCurrent)
        {
            ConfirmSwitch(selected);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_previousScreen ?? ScreensManager.FindScreen<Screen>("MainMenu"));
        }
    }

    private static Widget CreateInstanceItemWidget(object item)
    {
        var instance = (InstanceItem)item;
        var widget = (ContainerWidget)LoadWidget(
            null,
            ContentManager.Get<XElement>("Widgets/InstanceItem"),
            null);
        var idLabel = widget.Children.Find<LabelWidget>("InstanceItem.Id")!;
        idLabel.Text = instance.Id;
        idLabel.Color = instance.IsCurrent ? new Color(96, 220, 96) : Color.White;
        widget.Children.Find<LabelWidget>("InstanceItem.Status")!.Text = instance.IsCurrent
            ? Text("CurrentRunning")
            : instance.IsRunning
                ? Text("RunningElsewhere")
                : Text("NotRunning");
        widget.Children.Find<LabelWidget>("InstanceItem.RunMode")!.Text = instance.RunMode == RunModeType.HeadlessServer
            ? Text("HeadlessMode")
            : Text("GuiMode");
        return widget;
    }

    private void RefreshInstances()
    {
        _instancesList.ClearItems();
        foreach (var instanceId in StarterInstanceManager.ListInstances())
        {
            _instancesList.AddItem(new InstanceItem(
                instanceId,
                string.Equals(instanceId, StarterInstanceManager.Current.Id, StringComparison.OrdinalIgnoreCase),
                StarterInstanceManager.IsInstanceRunning(instanceId),
                StarterInstanceManager.GetRunMode(instanceId)));
        }
    }

    private void ShowCreateDialog()
    {
        DialogsManager.ShowDialog(
            null,
            new TextBoxDialog(
                Text("CreateTitle"),
                string.Empty,
                32,
                instanceId =>
                {
                    if (string.IsNullOrWhiteSpace(instanceId))
                    {
                        return;
                    }

                    if (Execute(new CreateInstanceCommand(instanceId.Trim())))
                    {
                        RefreshInstances();
                    }
                },
                false));
    }

    private void ConfirmDelete(InstanceItem instance)
    {
        DialogsManager.Confirm(
            string.Format(Text("ConfirmDelete"), instance.Id),
            button =>
            {
                if (button is MessageDialogButton.Button1 &&
                    Execute(new DeleteInstanceCommand(instance.Id)))
                {
                    RefreshInstances();
                }
            });
    }

    private static void ConfirmSwitch(InstanceItem instance)
    {
        DialogsManager.Confirm(
            string.Format(Text("ConfirmSwitch"), instance.Id),
            button =>
            {
                if (button is MessageDialogButton.Button1)
                {
                    Execute(new SwitchInstanceCommand(instance.Id));
                }
            });
    }

    private static bool Execute(IGameCommand command)
    {
        var result = CommandExecutor.ExecuteApplication(command, GameManager.Project);
        if (result.Success)
        {
            return true;
        }

        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                LanguageManager.Error,
                CommandText.Resolve(result),
                LanguageManager.Ok));
        return false;
    }

    private static string Text(string key) =>
        LanguageManager.GetContentWidgets(nameof(InstanceManagementScreen), key);

    private sealed record InstanceItem(
        string Id,
        bool IsCurrent,
        bool IsRunning,
        RunModeType RunMode);
}
