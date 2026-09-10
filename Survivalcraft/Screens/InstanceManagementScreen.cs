using System.Xml.Linq;

using Game.Commands;

namespace Game.Screens;

public class InstanceManagementScreen : Screen
{
    private enum InstanceAction
    {
        Create,
        Clone,
        Delete,
        Switch
    }

    private readonly ActionPanelWidget _actionPanel;
    private readonly ListPanelWidget _instancesList;

    private Screen? _previousScreen;

    public InstanceManagementScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/InstanceManagementScreen"));
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _instancesList = Children.Find<ListPanelWidget>("InstancesList")!;
        _actionPanel.ItemTextProvider = item => Text(item.ToString()!);
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
        [
            InstanceAction.Create,
            InstanceAction.Clone,
            InstanceAction.Delete,
            InstanceAction.Switch
        ]);
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
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_previousScreen ?? ScreensManager.FindScreen<Screen>("MainMenu"));
        }
    }

    private bool IsActionEnabled(object item)
    {
        var selected = _instancesList.SelectedItem as InstanceItem;
        return (InstanceAction)item switch
        {
            InstanceAction.Create => true,
            InstanceAction.Clone => selected?.CanClone == true,
            InstanceAction.Delete => selected is { IsCurrent: false, IsRunning: false },
            InstanceAction.Switch => selected is { IsCurrent: false },
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        var selected = _instancesList.SelectedItem as InstanceItem;
        switch ((InstanceAction)item)
        {
            case InstanceAction.Create:
                ShowCreateDialog();
                break;
            case InstanceAction.Clone when selected?.CanClone == true:
                ShowCloneDialog(selected);
                break;
            case InstanceAction.Delete when selected is { IsCurrent: false, IsRunning: false }:
                ConfirmDelete(selected);
                break;
            case InstanceAction.Switch when selected is { IsCurrent: false }:
                ConfirmSwitch(selected);
                break;
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
                StarterInstanceManager.CanCloneInstance(instanceId),
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

    private void ShowCloneDialog(InstanceItem source)
    {
        DialogsManager.ShowDialog(
            null,
            new TextBoxDialog(
                string.Format(Text("CloneTitle"), source.Id),
                string.Empty,
                32,
                targetInstanceId =>
                {
                    if (string.IsNullOrWhiteSpace(targetInstanceId))
                    {
                        return;
                    }

                    ExecuteLongOperation(
                        new CloneInstanceCommand(source.Id, targetInstanceId.Trim()),
                        RefreshInstances);
                },
                false));
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

    private void ExecuteLongOperation(
        IGameCommand command,
        Action? success)
    {
        var busyDialog = new CancellableBusyDialog(Text("Working"), false)
        {
            IsCancelButtonEnabled = false
        };
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() => CommandExecutor.ExecuteApplication(command, GameManager.Project))
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                DialogsManager.HideDialog(busyDialog);
                if (task.IsFaulted)
                {
                    Log.Error($"Instance operation failed: {task.Exception}");
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            LanguageManager.Error,
                            Text("OperationFailed"),
                            LanguageManager.Ok));
                    return;
                }

                var result = task.Result;
                if (!result.Success)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            LanguageManager.Error,
                            CommandText.Resolve(result),
                            LanguageManager.Ok));
                    return;
                }

                success?.Invoke();
            }));
    }

    private static string Text(string key) =>
        LanguageManager.GetContentWidgets(nameof(InstanceManagementScreen), key);

    private sealed record InstanceItem(
        string Id,
        bool IsCurrent,
        bool IsRunning,
        bool CanClone,
        RunModeType RunMode);
}
