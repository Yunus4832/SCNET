using System.Xml.Linq;

using Game.Content;

namespace Game.Screens;

public sealed class ContentRepositoryScreen : Screen
{
    private enum RepositoryAction
    {
        AddOrEdit,
        Delete,
        Toggle,
        MoveUp,
        MoveDown,
        Test
    }

    private readonly ActionPanelWidget _actionPanel;
    private readonly LabelWidget _statusLabel;
    private readonly ListPanelWidget _repositoryList;
    private bool _busy;
    private object[] _returnParameters = [];
    private string _returnScreenName = "Content";
    private BusyDialog? _testDialog;
    private CancellationTokenSource? _testCancellation;

    public ContentRepositoryScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ContentRepositoryScreen"));
        _repositoryList = Children.Find<ListPanelWidget>("RepositoryList")!;
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _repositoryList.ItemWidgetFactory = CreateRepositoryWidget;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = item => item is RepositoryAction.Delete ? new Color(150, 50, 35) : null;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
        [
            RepositoryAction.AddOrEdit,
            RepositoryAction.MoveUp,
            RepositoryAction.MoveDown,
            RepositoryAction.Test
        ]);
        _actionPanel.SetSecondaryItems(
        [
            RepositoryAction.Toggle,
            RepositoryAction.Delete
        ]);
    }

    public override void Enter(object[] parameters)
    {
        _returnScreenName = parameters.FirstOrDefault() as string ?? "Content";
        _returnParameters = parameters.Skip(1).ToArray();
        Refresh();
    }

    public override void Leave()
    {
        _testCancellation?.Cancel();
        _testCancellation = null;
        if (_testDialog is not null)
        {
            DialogsManager.HideDialog(_testDialog);
            _testDialog = null;
        }

        _busy = false;
        _actionPanel.ShowPrimaryItems();
        _repositoryList.SelectedItem = null;
    }

    public override void Update()
    {
        _actionPanel.IsEnabled = !_busy;
        _actionPanel.Refresh();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_returnScreenName, _returnParameters);
        }
    }

    private static Widget CreateRepositoryWidget(object item)
    {
        var repository = (ContentRepository)item;
        var widget = (ContainerWidget)LoadWidget(null,
            ContentManager.Get<XElement>("Widgets/ContentRepositoryItem"), null);
        widget.Children.Find<LabelWidget>("ContentRepositoryItem.Name")!.Text = repository.Name;
        widget.Children.Find<LabelWidget>("ContentRepositoryItem.Address")!.Text = repository.BaseUrl;
        widget.Children.Find<LabelWidget>("ContentRepositoryItem.State")!.Text = repository.IsEnabled
            ? Text("Enabled")
            : Text("Disabled");
        return widget;
    }

    private void Refresh(Guid? selectedId = null)
    {
        var repositories = SettingsManager.ContentRepositories.Snapshot();
        _repositoryList.ClearItems();
        foreach (var repository in repositories)
        {
            _repositoryList.AddItem(repository);
        }

        _repositoryList.SelectedItem = selectedId is null
            ? null
            : repositories.FirstOrDefault(repository => repository.Id == selectedId);
        _statusLabel.Text = repositories.Count == 0 ? Text("NoRepositories") : string.Empty;
    }

    private void ShowEditor(ContentRepository? repository)
    {
        DialogsManager.ShowDialog(null, new ContentRepositoryDialog(
            repository is null ? Text("AddTitle") : Text("EditTitle"),
            Text("NameLabel"),
            Text("AddressLabel"),
            repository is null ? Text("Add") : Text("Edit"),
            repository?.Name ?? string.Empty,
            repository?.BaseUrl ?? string.Empty,
            (name, address) => SaveEditor(repository, name, address)));
    }

    private bool SaveEditor(ContentRepository? repository, string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Text("NameRequired"));
            return false;
        }

        try
        {
            var current = SettingsManager.ContentRepositories.Snapshot();
            var candidate = (repository ?? new ContentRepository
            {
                Priority = current.Count
            }) with
            {
                Name = name,
                BaseUrl = address
            };
            var normalized = candidate.Normalize();
            if (current.Any(item => item.Id != normalized.Id && item.BaseUrl == normalized.BaseUrl))
            {
                ShowError(Text("DuplicateAddress"));
                return false;
            }

            if (repository is null)
            {
                SettingsManager.ContentRepositories.Add(normalized);
            }
            else
            {
                SettingsManager.ContentRepositories.Edit(normalized);
            }

            Refresh(normalized.Id);
            return true;
        }
        catch (ArgumentException)
        {
            ShowError(Text("InvalidAddress"));
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"Content repository update failed: {exception}");
            ShowError(Text("SaveFailed"));
            return false;
        }
    }

    private void ConfirmDelete(ContentRepository repository)
    {
        DialogsManager.Confirm(string.Format(Text("ConfirmDelete"), repository.Name), button =>
        {
            if (button == MessageDialogButton.Button1)
            {
                Execute(() => SettingsManager.ContentRepositories.Delete(repository.Id));
            }
        });
    }

    private void Move(IReadOnlyList<ContentRepository> repositories, int sourceIndex, int targetIndex)
    {
        var ids = repositories.Select(repository => repository.Id).ToList();
        (ids[sourceIndex], ids[targetIndex]) = (ids[targetIndex], ids[sourceIndex]);
        var selectedId = ids[targetIndex];
        Execute(() => SettingsManager.ContentRepositories.SetOrder(ids), selectedId);
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not RepositoryAction action)
        {
            return false;
        }

        var repositories = SettingsManager.ContentRepositories.Snapshot();
        var selected = _repositoryList.SelectedItem as ContentRepository;
        var selectedIndex = selected is null
            ? -1
            : repositories.ToList().FindIndex(repository => repository.Id == selected.Id);
        return action switch
        {
            RepositoryAction.AddOrEdit => true,
            RepositoryAction.Delete or RepositoryAction.Toggle => selected is not null,
            RepositoryAction.MoveUp => selectedIndex > 0,
            RepositoryAction.MoveDown => selectedIndex >= 0 && selectedIndex < repositories.Count - 1,
            RepositoryAction.Test => selected?.IsEnabled == true,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (item is not RepositoryAction action || !IsActionEnabled(action))
        {
            return;
        }

        var repositories = SettingsManager.ContentRepositories.Snapshot();
        var selected = _repositoryList.SelectedItem as ContentRepository;
        var selectedIndex = selected is null
            ? -1
            : repositories.ToList().FindIndex(repository => repository.Id == selected.Id);
        switch (action)
        {
            case RepositoryAction.AddOrEdit:
                ShowEditor(selected);
                break;
            case RepositoryAction.Delete when selected is not null:
                ConfirmDelete(selected);
                break;
            case RepositoryAction.Toggle when selected is not null:
                Execute(() => SettingsManager.ContentRepositories.Edit(selected with
                {
                    IsEnabled = !selected.IsEnabled
                }), selected.Id);
                break;
            case RepositoryAction.MoveUp:
                Move(repositories, selectedIndex, selectedIndex - 1);
                break;
            case RepositoryAction.MoveDown:
                Move(repositories, selectedIndex, selectedIndex + 1);
                break;
            case RepositoryAction.Test when selected is not null:
                TestConnection(selected);
                break;
        }
    }

    private string GetActionText(object item)
    {
        if (item is not RepositoryAction action)
        {
            return string.Empty;
        }

        var selected = _repositoryList.SelectedItem as ContentRepository;
        return action switch
        {
            RepositoryAction.AddOrEdit => selected is null ? Text("Add") : Text("Edit"),
            RepositoryAction.Toggle => selected?.IsEnabled == false ? Text("Enable") : Text("Disable"),
            _ => Text(action.ToString())
        };
    }

    private void TestConnection(ContentRepository repository)
    {
        _testCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _testCancellation = cancellation;
        _busy = true;
        var busyDialog = new BusyDialog(Text("Testing"), repository.Name);
        _testDialog = busyDialog;
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() => SettingsManager.ContentRepositories.TestConnectionAsync(repository.Id, cancellation.Token),
                cancellation.Token)
            .ContinueWith(task => Dispatcher.Dispatch(() =>
            {
                cancellation.Dispose();
                if (!ReferenceEquals(_testCancellation, cancellation))
                {
                    return;
                }

                _testCancellation = null;
                _testDialog = null;
                _busy = false;
                DialogsManager.HideDialog(busyDialog);
                if (task.IsCanceled)
                {
                    return;
                }

                if (task.IsCompletedSuccessfully)
                {
                    DialogsManager.Alert(string.Format(Text("TestSucceeded"),
                        task.Result.Name, task.Result.Version));
                }
                else
                {
                    Log.Error($"Content repository test failed: {task.Exception}");
                    ShowError(Text("TestFailed"));
                }
            }));
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
            Log.Error($"Content repository operation failed: {exception}");
            ShowError(Text("SaveFailed"));
        }
    }

    private static void ShowError(string message)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Error, message, LanguageManager.Ok));
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(nameof(ContentRepositoryScreen), key);
    }
}
