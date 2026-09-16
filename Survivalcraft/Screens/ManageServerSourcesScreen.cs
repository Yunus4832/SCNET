using System.Xml.Linq;

using Game.Servers;

using ServerSource.Protocol;

namespace Game.Screens;

public sealed class ManageServerSourcesScreen : Screen
{
    private enum SourceAction
    {
        AddOrEdit,
        Delete,
        Toggle,
        MoveUp,
        MoveDown,
        Test
    }

    private readonly ActionPanelWidget _actionPanel;
    private readonly ListPanelWidget _sourceList;
    private readonly LabelWidget _statusLabel;
    private bool _busy;
    private CancellationTokenSource? _testCancellation;

    public ManageServerSourcesScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ManageServerSourcesScreen"));
        _sourceList = Children.Find<ListPanelWidget>("SourceList")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _sourceList.ItemWidgetFactory = CreateSourceWidget;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = item => item is SourceAction.Delete ? new Color(150, 50, 35) : null;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
            [SourceAction.AddOrEdit, SourceAction.MoveUp, SourceAction.MoveDown, SourceAction.Test]);
        _actionPanel.SetSecondaryItems([SourceAction.Toggle, SourceAction.Delete]);
    }

    public override void Enter(object[] parameters)
    {
        Refresh();
    }

    public override void Leave()
    {
        _testCancellation?.Cancel();
        _testCancellation = null;
        _busy = false;
        _actionPanel.ShowPrimaryItems();
        _sourceList.SelectedItem = null;
    }

    public override void Update()
    {
        _actionPanel.IsEnabled = !_busy;
        _actionPanel.Refresh();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private static Widget CreateSourceWidget(object item)
    {
        var source = (InstalledServerSource)item;
        var widget = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        widget.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = source.Name;
        widget.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = source.ApiUrl;
        widget.Children.Find<LabelWidget>("ServerSourceItem.State")!.Text = source.IsEnabled
            ? CommonText("Enabled")
            : CommonText("Disabled");
        return widget;
    }

    private void Refresh(Guid? selectedId = null)
    {
        var sources = SettingsManager.ServerDirectory.Snapshot().InstalledSources;
        _sourceList.ClearItems();
        foreach (var source in sources)
        {
            _sourceList.AddItem(source);
        }

        _sourceList.SelectedItem = selectedId is null
            ? null
            : sources.FirstOrDefault(source => source.Id == selectedId);
        _statusLabel.Text = sources.Count == 0 ? Text("NoSources") : string.Empty;
    }

    private void ShowEditor(InstalledServerSource? source)
    {
        DialogsManager.ShowDialog(null, new ContentRepositoryDialog(
            source is null ? CommonText("AddTitle") : CommonText("EditTitle"),
            CommonText("NameLabel"), CommonText("AddressLabel"),
            source is null ? CommonText("Add") : CommonText("Edit"),
            source?.Name ?? string.Empty, source?.ApiUrl ?? string.Empty,
            (name, address) => SaveEditor(source, name, address)));
    }

    private bool SaveEditor(InstalledServerSource? source, string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Text("NameRequired"));
            return false;
        }

        try
        {
            var normalized = (source ?? new InstalledServerSource()) with { Name = name, ApiUrl = address };
            if (source is null)
            {
                normalized = SettingsManager.ServerDirectory.InstallSource(normalized);
            }
            else
            {
                SettingsManager.ServerDirectory.EditInstalledSource(normalized);
            }

            Refresh(normalized.Id);
            return true;
        }
        catch (ArgumentException exception)
        {
            var duplicate = exception.Message.Contains("already exists", StringComparison.Ordinal);
            ShowError(Text(duplicate ? "DuplicateAddress" : "InvalidAddress"));
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"Server source update failed: {exception}");
            ShowError(Text("SaveFailed"));
            return false;
        }
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not SourceAction action)
        {
            return false;
        }

        var sources = SettingsManager.ServerDirectory.Snapshot().InstalledSources;
        var selected = _sourceList.SelectedItem as InstalledServerSource;
        var index = selected is null ? -1 : sources.ToList().FindIndex(source => source.Id == selected.Id);
        return action switch
        {
            SourceAction.AddOrEdit => true,
            SourceAction.Delete or SourceAction.Toggle or SourceAction.Test => selected is not null,
            SourceAction.MoveUp => index > 0,
            SourceAction.MoveDown => index >= 0 && index < sources.Count - 1,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (item is not SourceAction action || !IsActionEnabled(action))
        {
            return;
        }

        var sources = SettingsManager.ServerDirectory.Snapshot().InstalledSources;
        var selected = _sourceList.SelectedItem as InstalledServerSource;
        var index = selected is null ? -1 : sources.ToList().FindIndex(source => source.Id == selected.Id);
        switch (action)
        {
            case SourceAction.AddOrEdit:
                ShowEditor(selected);
                break;
            case SourceAction.Delete when selected is not null:
                ConfirmDelete(selected);
                break;
            case SourceAction.Toggle when selected is not null:
                Execute(() => SettingsManager.ServerDirectory.EditInstalledSource(selected with
                {
                    IsEnabled = !selected.IsEnabled
                }), selected.Id);
                break;
            case SourceAction.MoveUp:
                Move(sources, index, index - 1);
                break;
            case SourceAction.MoveDown:
                Move(sources, index, index + 1);
                break;
            case SourceAction.Test when selected is not null:
                TestSource(selected);
                break;
        }
    }

    private void ConfirmDelete(InstalledServerSource source)
    {
        DialogsManager.Confirm(string.Format(Text("ConfirmDelete"), source.Name), button =>
        {
            if (button == MessageDialogButton.Button1)
            {
                Execute(() => SettingsManager.ServerDirectory.DeleteInstalledSource(source.Id));
            }
        });
    }

    private void Move(IReadOnlyList<InstalledServerSource> sources, int sourceIndex, int targetIndex)
    {
        var ids = sources.Select(source => source.Id).ToList();
        (ids[sourceIndex], ids[targetIndex]) = (ids[targetIndex], ids[sourceIndex]);
        Execute(() => SettingsManager.ServerDirectory.SetInstalledSourceOrder(ids), ids[targetIndex]);
    }

    private void TestSource(InstalledServerSource source)
    {
        _testCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _testCancellation = cancellation;
        _busy = true;
        var busyDialog = new BusyDialog(Text("Testing"), source.Name);
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(async () =>
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var protocolClient = new ServerSourceProtocolClient(httpClient);
                return await protocolClient.GetAllAsync(new Uri(source.ApiUrl), cancellation.Token);
            }, cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                cancellation.Dispose();
                if (!ReferenceEquals(_testCancellation, cancellation))
                {
                    return;
                }

                _testCancellation = null;
                _busy = false;
                DialogsManager.HideDialog(busyDialog);
                if (task.IsCanceled)
                {
                    return;
                }

                if (task.IsCompletedSuccessfully)
                {
                    DialogsManager.Alert(string.Format(Text("TestSucceeded"), task.Result.Source.Name,
                        task.Result.Servers.Count));
                }
                else
                {
                    Log.Error($"Server source validation failed: {task.Exception}");
                    ShowError(Text("TestFailed"));
                }
            }));
    }

    private string GetActionText(object item)
    {
        if (item is not SourceAction action)
        {
            return string.Empty;
        }

        var selected = _sourceList.SelectedItem as InstalledServerSource;
        return action switch
        {
            SourceAction.AddOrEdit => selected is null ? CommonText("Add") : CommonText("Edit"),
            SourceAction.Toggle => selected?.IsEnabled == false ? CommonText("Enable") : CommonText("Disable"),
            _ => CommonText(action.ToString())
        };
    }

    private void Execute(Action action, Guid? selectedId = null)
    {
        try
        {
            action();
            Refresh(selectedId);
        }
        catch (Exception exception)
        {
            Log.Error($"Server source operation failed: {exception}");
            ShowError(Text("SaveFailed"));
        }
    }

    private static void ShowError(string message)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Error, message, LanguageManager.Ok));
    }

    private static string CommonText(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(ContentRepositoryScreen), key);
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(ManageServerSourcesScreen), key);
    }
}
