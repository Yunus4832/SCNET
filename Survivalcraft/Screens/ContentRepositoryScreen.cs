using System.Xml.Linq;

using Game.Content;

namespace Game.Screens;

public sealed class ContentRepositoryScreen : Screen
{
    private readonly ButtonWidget _addButton;
    private readonly ButtonWidget _deleteButton;
    private readonly ButtonWidget _editButton;
    private readonly ButtonWidget _moveDownButton;
    private readonly ButtonWidget _moveUpButton;
    private readonly LabelWidget _statusLabel;
    private readonly ButtonWidget _testButton;
    private readonly ButtonWidget _toggleButton;
    private readonly ListPanelWidget _repositoryList;
    private bool _busy;
    private string _returnScreenName = "Content";
    private BusyDialog? _testDialog;
    private CancellationTokenSource? _testCancellation;

    public ContentRepositoryScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ContentRepositoryScreen"));
        _repositoryList = Children.Find<ListPanelWidget>("RepositoryList")!;
        _addButton = Children.Find<ButtonWidget>("Add")!;
        _editButton = Children.Find<ButtonWidget>("Edit")!;
        _deleteButton = Children.Find<ButtonWidget>("Delete")!;
        _toggleButton = Children.Find<ButtonWidget>("Toggle")!;
        _moveUpButton = Children.Find<ButtonWidget>("MoveUp")!;
        _moveDownButton = Children.Find<ButtonWidget>("MoveDown")!;
        _testButton = Children.Find<ButtonWidget>("Test")!;
        _statusLabel = Children.Find<LabelWidget>("Status")!;
        _repositoryList.ItemWidgetFactory = CreateRepositoryWidget;
    }

    public override void Enter(object[] parameters)
    {
        _returnScreenName = parameters.FirstOrDefault() as string ?? "Content";
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
        _repositoryList.SelectedItem = null;
    }

    public override void Update()
    {
        var repositories = SettingsManager.ContentRepositories.Snapshot();
        var selected = _repositoryList.SelectedItem as ContentRepository;
        var selectedIndex = selected is null
            ? -1
            : repositories.ToList().FindIndex(repository => repository.Id == selected.Id);
        _addButton.IsEnabled = !_busy;
        _editButton.IsEnabled = !_busy && selected is not null;
        _deleteButton.IsEnabled = !_busy && selected is not null;
        _toggleButton.IsEnabled = !_busy && selected is not null;
        _moveUpButton.IsEnabled = !_busy && selectedIndex > 0;
        _moveDownButton.IsEnabled = !_busy && selectedIndex >= 0 && selectedIndex < repositories.Count - 1;
        _testButton.IsEnabled = !_busy && selected?.IsEnabled == true;
        _toggleButton.Text = selected?.IsEnabled == false ? Text("Enable") : Text("Disable");

        if (_addButton.IsClicked)
        {
            ShowEditor(null);
        }

        if (_editButton.IsClicked && selected is not null)
        {
            ShowEditor(selected);
        }

        if (_deleteButton.IsClicked && selected is not null)
        {
            ConfirmDelete(selected);
        }

        if (_toggleButton.IsClicked && selected is not null)
        {
            Execute(() => SettingsManager.ContentRepositories.Edit(selected with
            {
                IsEnabled = !selected.IsEnabled
            }), selected.Id);
        }

        if (_moveUpButton.IsClicked && selectedIndex > 0)
        {
            Move(repositories, selectedIndex, selectedIndex - 1);
        }

        if (_moveDownButton.IsClicked && selectedIndex >= 0 && selectedIndex < repositories.Count - 1)
        {
            Move(repositories, selectedIndex, selectedIndex + 1);
        }

        if (_testButton.IsClicked && selected is not null)
        {
            TestConnection(selected);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_returnScreenName);
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
        DialogsManager.ShowDialog(null, new TextBoxDialog(
            repository is null ? Text("AddTitle") : Text("EditNameTitle"),
            repository?.Name ?? string.Empty,
            64,
            name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    ShowError(Text("NameRequired"));
                    return;
                }

                ShowAddressEditor(repository, name.Trim());
            }, false));
    }

    private void ShowAddressEditor(ContentRepository? repository, string name)
    {
        DialogsManager.ShowDialog(null, new TextBoxDialog(
            Text("AddressTitle"),
            repository?.BaseUrl ?? "https://",
            TemporaryContentRepositories.MaximumUrlLength,
            address => SaveEditor(repository, name, address), false));
    }

    private void SaveEditor(ContentRepository? repository, string name, string address)
    {
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
                return;
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
        }
        catch (ArgumentException)
        {
            ShowError(Text("InvalidAddress"));
        }
        catch (Exception exception)
        {
            Log.Error($"Content repository update failed: {exception}");
            ShowError(Text("SaveFailed"));
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
