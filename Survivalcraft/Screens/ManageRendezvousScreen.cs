using System.Xml.Linq;

using Engine.Network.Rendezvous;

using Game.Servers;

namespace Game.Screens;

public sealed class ManageRendezvousScreen : Screen
{
    private enum SourceAction
    {
        AddOrEdit = 0,

        Delete = 1,

        Toggle = 2,

        MoveUp = 3,

        MoveDown = 4,

        Test = 5
    }

    private readonly ActionPanelWidget _actionPanel;

    private readonly ListPanelWidget _sourceList;

    private readonly LabelWidget _statusLabel;

    private bool _busy;

    private string _returnScreen = "Content";

    private object[] _returnParameters = [];

    private CancellableBusyDialog? _busyDialog;

    private ContentRepositoryDialog? _editor;

    private CancellationTokenSource? _testCancellation;

    public ManageRendezvousScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ManageRendezvousScreen"));
        _sourceList = Children.Find<ListPanelWidget>("SourceList")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _sourceList.ItemWidgetFactory = CreateSourceWidget;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = item => item is SourceAction.Delete ? new Color(150, 50, 35) : null;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
            [SourceAction.AddOrEdit, SourceAction.MoveUp, SourceAction.MoveDown, SourceAction.Test]
        );
        _actionPanel.SetSecondaryItems([SourceAction.Toggle, SourceAction.Delete]);
    }

    public override void Enter(object[] parameters)
    {
        _returnScreen = parameters.Length > 0 && parameters[0] is string screen ? screen : "Content";
        _returnParameters = parameters.Length > 1 && parameters[1] is object[] arguments ? arguments : [];
        Refresh();
    }

    public override void Leave()
    {
        _testCancellation?.Cancel();
        _testCancellation = null;
        if (_busyDialog is not null)
        {
            DialogsManager.HideDialog(_busyDialog);
            _busyDialog = null;
        }

        if (_editor is not null)
        {
            DialogsManager.HideDialog(_editor);
            _editor = null;
        }

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
            ScreensManager.SwitchScreen(_returnScreen, _returnParameters);
        }
    }

    private static Widget CreateSourceWidget(object item)
    {
        var source = (InstalledRendezvousService)item;
        var widget = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        widget.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = source.Name;
        widget.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = source.ControlUrl;
        widget.Children.Find<LabelWidget>("ServerSourceItem.State")!.Text = source.IsEnabled
            ? CommonText("Enabled")
            : CommonText("Disabled");
        return widget;
    }

    private void Refresh(Guid? selectedId = null)
    {
        var sources = RendezvousManager.Current.Snapshot();
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

    private void ShowEditor(InstalledRendezvousService? source)
    {
        _editor = new ContentRepositoryDialog(
            source is null ? Text("AddTitle") : Text("EditTitle"),
            CommonText("NameLabel"),
            CommonText("AddressLabel"),
            source is null ? CommonText("Add") : CommonText("Save"),
            source?.Name ?? string.Empty,
            source?.ControlUrl ?? string.Empty,
            (name, address) => SaveEditor(source, name, address),
            nameMaximumLength: 100
        );
        DialogsManager.ShowDialog(null, _editor);
    }

    private bool SaveEditor(InstalledRendezvousService? source, string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Text("NameRequired"));
            return false;
        }

        try
        {
            var addressUri = RendezvousAddress.Normalize(new Uri(address));
            var normalized = (source ?? new InstalledRendezvousService()) with
            {
                Name = name, ControlUrl = addressUri.AbsoluteUri
            };
            if (RendezvousManager.Current.Snapshot()
                .Any(item => item.Id != normalized.Id && item.ControlUrl == normalized.ControlUrl))
            {
                ShowError(Text("DuplicateAddress"));
                return false;
            }

            RunProbe(
                normalized,
                () =>
                {
                    if (source is null)
                    {
                        RendezvousManager.Current.Add(normalized);
                    }
                    else
                    {
                        RendezvousManager.Current.Edit(normalized);
                    }

                    if (_editor is not null)
                    {
                        DialogsManager.HideDialog(_editor);
                        _editor = null;
                    }

                    Refresh(normalized.Id);
                }
            );
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            var duplicate = exception.Message.Contains("already exists", StringComparison.Ordinal);
            ShowError(Text(duplicate ? "DuplicateAddress" : "InvalidAddress"));
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"Signaling service update failed: {exception}");
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

        var sources = RendezvousManager.Current.Snapshot();
        var selected = _sourceList.SelectedItem as InstalledRendezvousService;
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

        var sources = RendezvousManager.Current.Snapshot();
        var selected = _sourceList.SelectedItem as InstalledRendezvousService;
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
                Execute(
                    () => RendezvousManager.Current.Edit(
                        selected with
                        {
                            IsEnabled = !selected.IsEnabled
                        }
                    ),
                    selected.Id
                );
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

    private void ConfirmDelete(InstalledRendezvousService source)
    {
        DialogsManager.Confirm(
            string.Format(Text("ConfirmDelete"), source.Name),
            button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    Execute(() => RendezvousManager.Current.Delete(source.Id));
                }
            }
        );
    }

    private void Move(IReadOnlyList<InstalledRendezvousService> sources, int sourceIndex, int targetIndex)
    {
        var ids = sources.Select(source => source.Id).ToList();
        (ids[sourceIndex], ids[targetIndex]) = (ids[targetIndex], ids[sourceIndex]);
        Execute(() => RendezvousManager.Current.SetOrder(ids), ids[targetIndex]);
    }

    private void TestSource(InstalledRendezvousService source) =>
        RunProbe(source, () => DialogsManager.Alert(Text("TestSucceeded")));

    private void RunProbe(InstalledRendezvousService source, Action success)
    {
        if (_busy)
        {
            return;
        }

        var dialog = new CancellableBusyDialog(Text("Testing"), true);
        var cancellation = new CancellationTokenSource();
        dialog.CancelRequested += cancellation.Cancel;
        _testCancellation = cancellation;
        _busyDialog = dialog;
        _busy = true;
        DialogsManager.ShowDialog(null, dialog);
        Task.Run(() => RendezvousServiceProbe.TestAsync(source.ControlUrl, cancellation.Token), cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() =>
                    {
                        if (!ReferenceEquals(_testCancellation, cancellation))
                        {
                            _ = task.Exception;
                            dialog.CancelRequested -= cancellation.Cancel;
                            cancellation.Dispose();
                            return;
                        }

                        _testCancellation = null;
                        _busyDialog = null;
                        _busy = false;
                        DialogsManager.HideDialog(dialog);
                        dialog.CancelRequested -= cancellation.Cancel;
                        cancellation.Dispose();
                        if (task.IsCanceled)
                        {
                            return;
                        }

                        if (!task.IsCompletedSuccessfully)
                        {
                            Log.Warning(
                                $"Signaling service validation failed: {task.Exception?.GetBaseException().Message}"
                            );
                            ShowError(Text("TestFailed"));
                            return;
                        }

                        Execute(success, source.Id);
                    }
                )
            );
    }

    private string GetActionText(object item)
    {
        if (item is not SourceAction action)
        {
            return string.Empty;
        }

        var selected = _sourceList.SelectedItem as InstalledRendezvousService;
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
            Log.Error($"Signaling service operation failed: {exception}");
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
        return LanguageManager.GetContentWidgets(nameof(ManageRendezvousScreen), key);
    }
}
