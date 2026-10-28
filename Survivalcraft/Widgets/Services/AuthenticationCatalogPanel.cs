using System.Globalization;
using System.Xml.Linq;

using Game.Authentication;
using Game.Content;
using Game.Identity;

using SCNET.Authentication.Protocol.Http;

namespace Game.Widgets.Services;

public sealed class AuthenticationCatalogPanel : ServicePanelWidget
{
    private enum CatalogAction { Refresh = 0, Install = 1, Info = 2 }

    private sealed record RepositoryOption(Guid? Id, string Name);

    private readonly AuthenticationManager _manager;

    private readonly ActionPanelWidget _actions;

    private readonly SelectionDrawerWidget _repositories;

    private readonly TextBoxWidget _search;

    private readonly LabelWidget _placeholder;

    private readonly ListPanelWidget _list;

    private readonly LabelWidget _status;

    private readonly LabelWidget _warnings;

    private IReadOnlyList<RegisteredAuthenticationService> _services = [];

    private IReadOnlyList<ContentRepository> _configured = [];

    private IReadOnlySet<Guid> _unavailable = new HashSet<Guid>();

    private CancellationTokenSource? _cancel;

    private Task? _operation;

    private Action? _finish;

    private CancellableBusyDialog? _busy;

    private MessageDialog? _message;

    private int _generation;

    private int _operationGeneration;

    public AuthenticationCatalogPanel() : this(AuthenticationManager.Current)
    {
    }

    public AuthenticationCatalogPanel(AuthenticationManager manager)
    {
        _manager = manager;
        LoadContents(this, ContentManager.Get<XElement>("Widgets/Services/AuthenticationCatalogPanel"));
        _actions = Children.Find<ActionPanelWidget>("Actions")!;
        _repositories = Children.Find<SelectionDrawerWidget>("RepositoryFilter")!;
        _search = Children.Find<TextBoxWidget>("Search")!;
        _placeholder = Children.Find<LabelWidget>("SearchPlaceholder")!;
        _list = Children.Find<ListPanelWidget>("SourceList")!;
        _status = Children.Find<LabelWidget>("Status")!;
        _warnings = Children.Find<LabelWidget>("Warnings")!;
        _list.ItemWidgetFactory = CreateRow;
        _search.MaximumLength = 100;
        _search.TextChanged += _ => ApplySearch();
        _repositories.ItemTextProvider = item => ((RepositoryOption)item).Name;
        _repositories.SelectionChanged += ApplySearch;
        _actions.ItemTextProvider = item => Text(item.ToString()!);
        _actions.ItemEnabledProvider =
            item => _operation is null &&
                    (item is CatalogAction.Refresh ||
                     Selected is { } source && (item is CatalogAction.Info || !IsInstalled(source)));
        _actions.ItemColorProvider = item => item is CatalogAction.Install ? new Color(50, 150, 35) : null;
        _actions.ItemClicked += item =>
        {
            if (item is CatalogAction.Refresh) { Load(); }
            else if (item is CatalogAction.Info && Selected is { } source) { ShowMessage(Details(source), null); }
            else { Install(); }
        };
        _actions.SetPrimaryItems([CatalogAction.Install, CatalogAction.Info, CatalogAction.Refresh], [1f, 1f, 1f, 0f]);
    }

    private RegisteredAuthenticationService? Selected => _list.SelectedItem as RegisteredAuthenticationService;

    public override void Activate()
    {
        _generation++;
        _placeholder.Text = Text("SearchPlaceholder");
        _search.Text = string.Empty;
        Load();
    }

    public override void Deactivate()
    {
        _generation++;
        _cancel?.Cancel();
        _cancel?.Dispose();
        _cancel = null;
        _operation = null;
        _finish = null;
        if (_busy is not null)
        {
            DialogsManager.HideDialog(_busy);
            _busy = null;
        }

        if (_message is not null)
        {
            DialogsManager.HideDialog(_message);
            _message = null;
        }

        _repositories.Close();
        _list.ClearItems();
        _services = [];
    }

    public override void Update()
    {
        if (_operation is { IsCompleted: true } task)
        {
            var cancelled = _cancel!.IsCancellationRequested;
            _cancel.Dispose();
            _cancel = null;
            _operation = null;
            var finish = _finish;
            _finish = null;
            if (_busy is not null)
            {
                DialogsManager.HideDialog(_busy);
                _busy = null;
            }

            if (_operationGeneration == _generation && !cancelled)
            {
                if (task.IsCompletedSuccessfully) { finish?.Invoke(); }
                else { ShowMessage(Text("Failed"), null); }
            }
        }

        _actions.Refresh();
    }

    private void Load()
    {
        if (_operation is not null) { return; }

        _configured = ContentRepositoryManager.Current.Snapshot();
        Run(
            token => new ContentAuthenticationCatalog(ContentServerClientPool.Shared).QueryAsync(_configured, token),
            result =>
            {
                _services = result.Services.OrderBy(source => source.RepositoryName).ThenBy(source => source.Name)
                    .ToArray();
                _unavailable = result.UnavailableRepositories;
                var options = new List<RepositoryOption> { new(null, Text("AllRepositories")) };
                options.AddRange(
                    _configured.Where(repository => repository.IsEnabled).Select(repository =>
                        new RepositoryOption(repository.Id, repository.Name)
                    )
                );
                _repositories.SetItems(options);
                _repositories.SelectedItem = options[0];
                ApplySearch();
            }
        );
    }

    private void ApplySearch()
    {
        _placeholder.IsVisible = _search.Text.Length == 0;
        var selected = Selected;
        var repository = (_repositories.SelectedItem as RepositoryOption)?.Id;
        var query = _search.Text.Trim();
        var visible = _services.Where(source => (repository is null || source.RepositoryId == repository) &&
                                                (query.Length == 0 ||
                                                 new[]
                                                 {
                                                     source.Name, source.ServiceUrl,
                                                     source.Description ?? string.Empty
                                                 }.Any(value => value.Contains(
                                                         query,
                                                         StringComparison.OrdinalIgnoreCase
                                                     )
                                                 ))
        ).ToArray();
        _list.ClearItems();
        foreach (var source in visible) { _list.AddItem(source); }

        _list.SelectedItem = visible.FirstOrDefault(source => source == selected);
        var unavailable = repository is { } id ? _unavailable.Contains(id) : _unavailable.Count > 0;
        _status.Text = visible.Length > 0
            ? string.Empty
            : Text(_configured.All(item => !item.IsEnabled) ? "NoRepositories" : unavailable ? "Unavailable" : "Empty");
        _warnings.IsVisible = unavailable;
        _warnings.Text = Text("SomeUnavailable");
    }

    private Widget CreateRow(object item)
    {
        var source = (RegisteredAuthenticationService)item;
        var row = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        row.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = source.Name;
        row.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = source.ServiceUrl;
        row.Children.Find<LabelWidget>("ServerSourceItem.State")!.Text =
            IsInstalled(source) ? Text("Installed") : source.RepositoryName;
        return row;
    }

    private bool IsInstalled(RegisteredAuthenticationService source)
    {
        return _manager.Services.Services.Any(service =>
            service.Endpoint.BaseUri == new Uri(source.ServiceUrl)
        );
    }

    private string Details(RegisteredAuthenticationService source)
    {
        return source.Name +
               "\n" +
               source.ServiceUrl +
               "\n" +
               source.RepositoryName +
               "\n" +
               source.RepositoryUrl +
               "\n" +
               source.ReviewedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
               "\n" +
               source.Description;
    }

    private void Install()
    {
        if (_operation is not null || Selected is not { } source) { return; }

        Run(
            token => AuthenticationHttpClient.DiscoverAsync(source.ServiceUrl, cancellationToken: token),
            endpoint =>
            {
                var state = _manager.GetInstallationStatus(endpoint);
                if (state is not (AuthenticationServiceInstallationStatus.Available
                    or AuthenticationServiceInstallationStatus.EndpointChange))
                {
                    ShowMessage(Text(state.ToString()), null);
                    return;
                }

                ShowMessage(
                    Details(source) +
                    "\n" +
                    endpoint.Description.DisplayName +
                    "\n" +
                    IdentityAddressDisplay.Abbreviate(endpoint.Identity.Address) +
                    "\n" +
                    Text(
                        state == AuthenticationServiceInstallationStatus.EndpointChange ? "AddressWarning" : "Confirm"
                    ),
                    () =>
                    {
                        _manager.InstallConfirmedService(endpoint, source.RepositoryUrl);
                        ApplySearch();
                    }
                );
            }
        );
    }

    private void Run<T>(Func<CancellationToken, Task<T>> operation, Action<T> finish)
    {
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _operationGeneration = _generation;
        var task = Task.Run(() => operation(token), token);
        _operation = task;
        _finish = () => finish(task.GetAwaiter().GetResult());
        _ = task.ContinueWith(failed => { _ = failed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _busy = new CancellableBusyDialog(Text("Working"), false);
        _busy.CancelRequested += () => _cancel?.Cancel();
        DialogsManager.ShowDialog(null, _busy);
    }

    private void ShowMessage(string description, Action? accepted)
    {
        var generation = _generation;
        _message = new MessageDialog(
            Text("Title"),
            description,
            LanguageManager.Get("Usual", "ok"),
            accepted is null ? string.Empty : LanguageManager.Get("Usual", "cancel"),
            button =>
            {
                _message = null;
                if (generation != _generation || button != MessageDialogButton.Button1) { return; }

                try { accepted?.Invoke(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                                  or InvalidOperationException)
                {
                    ShowMessage(Text("SaveFailed"), null);
                }
            }
        );
        DialogsManager.ShowDialog(null, _message);
    }

    private static string Text(string key) =>
        LanguageManager.GetContentWidgets(nameof(AuthenticationCatalogPanel), key);
}
