using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Identity;
using Game.Managers;

namespace Game.Screens;

public sealed class SettingsIdentityScreen : Screen
{
    private enum IdentityAction
    {
        Use = 0,

        Create = 1,

        Import = 2,

        Export = 3,

        Rename = 4,

        Details = 5,

        Delete = 6,

        ForgetPasswords = 7
    }

    private readonly LocalIdentityManager _identities;

    private readonly AuthenticationManager _authentication;

    private readonly ButtonWidget _playerTab;

    private readonly ButtonWidget _serverTab;

    private readonly ListPanelWidget _list;

    private readonly ActionPanelWidget _actions;

    private readonly LabelWidget _hint;

    private IdentityPurpose _purpose = IdentityPurpose.Player;

    private bool _busy;

    private int _generation;

    private byte[]? _importBackup;

    private char[]? _password;

    private PasswordDialog? _passwordDialog;

    public SettingsIdentityScreen() : this(LocalIdentityManager.Current, AuthenticationManager.Current)
    {
    }

    public SettingsIdentityScreen(LocalIdentityManager identities, AuthenticationManager authentication)
    {
        _identities = identities;
        _authentication = authentication;
        LoadContents(this, ContentManager.Get<XElement>("Screens/SettingsIdentityScreen"));
        _playerTab = Children.Find<ButtonWidget>("PlayerTab")!;
        _serverTab = Children.Find<ButtonWidget>("ServerTab")!;
        _list = Children.Find<ListPanelWidget>("Identities")!;
        _actions = Children.Find<ActionPanelWidget>("Actions")!;
        _hint = Children.Find<LabelWidget>("Hint")!;
        _list.ItemWidgetFactory = CreateIdentityRow;
        _actions.ItemTextProvider = item => Text(item.ToString()!);
        _actions.ItemEnabledProvider = IsActionEnabled;
        _actions.ItemColorProvider = item => item is IdentityAction.Delete ? new Color(150, 50, 35) :
            item is IdentityAction.Use ? new Color(45, 110, 60) : null;
        _actions.ItemClicked += ExecuteAction;
        _actions.SetPrimaryItems(
            [IdentityAction.Use, IdentityAction.Create, IdentityAction.Import, IdentityAction.Export]
        );
        _actions.SetSecondaryItems(
            [IdentityAction.Rename, IdentityAction.Details, IdentityAction.Delete, IdentityAction.ForgetPasswords]
        );
        RefreshIdentities();
    }

    private IdentityPurpose Purpose => _purpose;

    private LocalIdentityEntry? Selected => _list.SelectedItem as LocalIdentityEntry;

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(SettingsIdentityScreen), key);

    public override void Enter(object[] parameters)
    {
        _generation++;
        _actions.ShowPrimaryItems();
        RefreshIdentities();
    }

    public override void Leave()
    {
        _generation++;
        _passwordDialog?.Cancel();
        ClearImport();
        _busy = false;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        Refresh();
        if (_busy) { return; }

        if (_playerTab.IsClicked) { SelectTab(IdentityPurpose.Player); }

        if (_serverTab.IsClicked) { SelectTab(IdentityPurpose.Server); }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("MainMenu");
        }
    }

    private void SelectTab(IdentityPurpose purpose)
    {
        _purpose = purpose;
        _list.SelectedItem = null;
        _actions.ShowPrimaryItems();
        RefreshIdentities();
    }

    private Widget CreateIdentityRow(object item)
    {
        var identity = (LocalIdentityEntry)item;
        var row = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/IdentityItem"), null);
        row.Children.Find<LabelWidget>("Name")!.Text = IdentityName(identity);
        row.Children.Find<LabelWidget>("Address")!.Text = IdentityAddressDisplay.Abbreviate(identity.Address);
        row.Children.Find<LabelWidget>("Current")!.Text = identity.Address == _identities.GetSelectedAddress(Purpose)
            ? Text("Current")
            : string.Empty;
        return row;
    }

    private static string IdentityName(LocalIdentityEntry identity) => identity.Name.Length > 0
        ? identity.Name
        : Text(identity.Purpose == IdentityPurpose.Player ? "Player" : "Server");

    private void RefreshIdentities(string? selectAddress = null)
    {
        selectAddress ??= Selected?.Address ?? _identities.GetSelectedAddress(Purpose);
        _list.ClearItems();
        foreach (var identity in _identities.GetIdentities(Purpose))
        {
            _list.AddItem(identity);
            if (identity.Address == selectAddress) { _list.SelectedItem = identity; }
        }

        Refresh();
    }

    private void Refresh()
    {
        _playerTab.IsChecked = Purpose == IdentityPurpose.Player;
        _serverTab.IsChecked = Purpose == IdentityPurpose.Server;
        _playerTab.IsEnabled = !_busy;
        _serverTab.IsEnabled = !_busy;
        _list.IsEnabled = !_busy;
        _hint.Text = Text(!LocalIdentityManager.CanManage ? "OfflineRequired" : "SecurityHint");
        _actions.Refresh();
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy) { return false; }

        var editable = LocalIdentityManager.CanManage;
        return (IdentityAction)item switch
        {
            IdentityAction.Create => editable &&
                                     _identities.GetIdentities(Purpose).Count <
                                     LocalIdentityManager.MaximumIdentityCount,
            IdentityAction.Import => editable &&
                                     FilePicker.IsAvailable &&
                                     _identities.GetIdentities(Purpose).Count <
                                     LocalIdentityManager.MaximumIdentityCount,
            IdentityAction.Export => Selected is not null && FilePicker.IsAvailable,
            IdentityAction.Use or IdentityAction.Delete => editable &&
                                                           Selected is { } selected &&
                                                           selected.Address != _identities.GetSelectedAddress(Purpose),
            IdentityAction.Rename => editable && Selected is not null,
            IdentityAction.Details => Selected is not null,
            IdentityAction.ForgetPasswords => _authentication.ServerCredentials.Count > 0,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (!IsActionEnabled(item)) { return; }

        var selected = Selected;
        var purpose = Purpose;
        var generation = _generation;
        switch ((IdentityAction)item)
        {
            case IdentityAction.Create:
                AskName(string.Empty, name => RefreshIdentities(_identities.Create(purpose, name)));
                break;
            case IdentityAction.Use:
                try
                {
                    _identities.Select(purpose, selected!.Address);
                    RefreshIdentities();
                }
                catch { DialogsManager.Alert(Text("OperationFailed")); }

                break;
            case IdentityAction.Rename:
                AskName(
                    selected!.Name,
                    name =>
                    {
                        _identities.Rename(purpose, selected.Address, name);
                        RefreshIdentities();
                    }
                );
                break;
            case IdentityAction.Delete:
                DialogsManager.Confirm(
                    string.Format(Text("DeleteConfirm"), IdentityName(selected!)),
                    button =>
                    {
                        if (button != MessageDialogButton.Button1 ||
                            !IsCurrent(generation) ||
                            !LocalIdentityManager.CanManage) { return; }

                        try
                        {
                            _identities.Delete(purpose, selected!.Address);
                            RefreshIdentities();
                        }
                        catch { DialogsManager.Alert(Text("OperationFailed")); }
                    },
                    this
                );
                break;
            case IdentityAction.Details:
                DialogsManager.ShowDialog(
                    this,
                    new MessageDialog(
                        IdentityName(selected!),
                        selected!.Address,
                        Text("Copy"),
                        LanguageManager.Get("Usual", "close"),
                        button =>
                        {
                            if (button == MessageDialogButton.Button1)
                            {
                                ClipboardManager.ClipboardString = selected.Address;
                            }
                        }
                    )
                );
                break;
            case IdentityAction.Export:
                BeginExport();
                break;
            case IdentityAction.Import:
                BeginImport();
                break;
            case IdentityAction.ForgetPasswords:
                DialogsManager.Confirm(
                    Text("ForgetPasswordsConfirm"),
                    button =>
                    {
                        if (button != MessageDialogButton.Button1 || !IsCurrent(generation)) { return; }

                        try { _authentication.ServerCredentials.Clear(); }
                        catch { DialogsManager.Alert(Text("ForgetPasswordsFailed")); }
                    },
                    this
                );
                break;
        }
    }

    private void AskName(string initial, Action<string> accepted)
    {
        var generation = _generation;
        DialogsManager.ShowDialog(
            this,
            new TextBoxDialog(
                Text("Name"),
                initial,
                LocalIdentityManager.MaximumNameLength,
                name =>
                {
                    if (string.IsNullOrWhiteSpace(name) || !IsCurrent(generation) || !LocalIdentityManager.CanManage)
                    {
                        return;
                    }

                    try { accepted(name.Trim()); }
                    catch { DialogsManager.Alert(Text("OperationFailed")); }
                },
                false
            )
        );
    }

    private bool IsCurrent(int generation) => generation == _generation && ScreensManager.CurrentScreen == this;

    private void BeginExport()
    {
        _busy = true;
        var generation = _generation;
        var purpose = Purpose;
        var address = Selected!.Address;
        _passwordDialog = new PasswordDialog(
            Text("ExportPassword"),
            true,
            password =>
            {
                _passwordDialog = null;
                if (password is null)
                {
                    _busy = false;
                    return;
                }

                if (!IsCurrent(generation))
                {
                    Array.Clear(password);
                    return;
                }

                Export(purpose, address, password, generation);
            }
        );
        DialogsManager.ShowDialog(this, _passwordDialog);
    }

    private async void Export(IdentityPurpose purpose, string address, char[] password, int generation)
    {
        byte[]? backup = null;
        try
        {
            try { backup = await Task.Run(() => _identities.ExportBackup(purpose, address, password)); }
            finally { Array.Clear(password); }

            if (!IsCurrent(generation)) { return; }

            var target = await PickExportTarget(purpose, address, generation);
            if (target is null || !IsCurrent(generation)) { return; }

            await using var stream = await target.OpenWriteAsync(CancellationToken.None);
            await stream.WriteAsync(backup);
            await stream.FlushAsync();
            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrent(generation)) { DialogsManager.Alert(Text("ExportComplete")); }
                }
            );
        }
        catch { Dispatcher.Dispatch(() => ShowError(generation, "ExportFailed")); }
        finally
        {
            if (backup is not null) { CryptographicOperations.ZeroMemory(backup); }

            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrent(generation)) { _busy = false; }
                }
            );
        }
    }

    private Task<PickedSaveTarget?> PickExportTarget(IdentityPurpose purpose, string address, int generation)
    {
        var completion =
            new TaskCompletionSource<PickedSaveTarget?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(async () =>
            {
                try
                {
                    if (!IsCurrent(generation))
                    {
                        completion.SetResult(null);
                        return;
                    }

                    var target = await FilePicker.PickSaveTargetAsync(
                        new FileSaveRequest(
                            $"{purpose.ToString().ToLowerInvariant()}-{address}.p8",
                            "application/octet-stream",
                            Text("Export")
                        )
                    );
                    completion.SetResult(target);
                }
                catch (Exception exception) { completion.SetException(exception); }
            }
        );
        return completion.Task;
    }

    private async void BeginImport()
    {
        _busy = true;
        var generation = _generation;
        var purpose = Purpose;
        byte[]? backup = null;
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest([".p8"], false, Text("Import")));
            if (files.Count == 0 || !IsCurrent(generation))
            {
                Dispatcher.Dispatch(() => FinishImport(generation));
                return;
            }

            await using var source = await files[0].OpenReadAsync(CancellationToken.None);
            using var output = new MemoryStream();
            var buffer = new byte[1024];
            while (true)
            {
                var read = await source.ReadAsync(buffer);
                if (read == 0) { break; }

                if (output.Length + read > 16384) { throw new InvalidDataException("Identity backup is too large."); }

                output.Write(buffer, 0, read);
            }

            backup = output.ToArray();
            if (!IsCurrent(generation)) { return; }

            var captured = backup;
            Dispatcher.Dispatch(() =>
                {
                    if (!IsCurrent(generation))
                    {
                        CryptographicOperations.ZeroMemory(captured);
                        return;
                    }

                    _importBackup = captured;
                    AskImportPassword(purpose, generation);
                }
            );
            backup = null;
        }
        catch
        {
            Dispatcher.Dispatch(() =>
                {
                    ShowError(generation, "ImportFailed");
                    FinishImport(generation);
                }
            );
        }
        finally
        {
            if (backup is not null) { CryptographicOperations.ZeroMemory(backup); }
        }
    }

    private void AskImportPassword(IdentityPurpose purpose, int generation)
    {
        _passwordDialog = new PasswordDialog(
            Text("ImportPassword"),
            false,
            password =>
            {
                _passwordDialog = null;
                if (password is null || !IsCurrent(generation))
                {
                    if (password is not null) { Array.Clear(password); }

                    FinishImport(generation);
                    return;
                }

                _password = password;
                try
                {
                    var backup = _importBackup!;
                    var address = LocalIdentityManager.InspectBackup(backup, password);
                    DialogsManager.Confirm(
                        string.Format(Text("ImportConfirm"), IdentityAddressDisplay.Abbreviate(address)),
                        button =>
                        {
                            if (button != MessageDialogButton.Button1 || !IsCurrent(generation))
                            {
                                FinishImport(generation);
                                return;
                            }

                            DialogsManager.ShowDialog(
                                this,
                                new TextBoxDialog(
                                    Text("Name"),
                                    Text("RestoredName"),
                                    LocalIdentityManager.MaximumNameLength,
                                    name =>
                                    {
                                        try
                                        {
                                            if (!IsCurrent(generation) ||
                                                string.IsNullOrWhiteSpace(name) ||
                                                !LocalIdentityManager.CanManage) { return; }

                                            var imported = _identities.ImportBackup(purpose, backup, password, name);
                                            RefreshIdentities(imported);
                                            DialogsManager.Alert(Text("ImportComplete"));
                                        }
                                        catch { ShowError(generation, "ImportFailed"); }
                                        finally { FinishImport(generation); }
                                    },
                                    false
                                )
                            );
                        },
                        this
                    );
                }
                catch
                {
                    ShowError(generation, "ImportFailed");
                    FinishImport(generation);
                }
            }
        );
        DialogsManager.ShowDialog(this, _passwordDialog);
    }

    private void ShowError(int generation, string key)
    {
        if (IsCurrent(generation)) { DialogsManager.Alert(Text(key)); }
    }

    private void FinishImport(int generation)
    {
        if (!IsCurrent(generation)) { return; }

        ClearImport();
        _busy = false;
    }

    private void ClearImport()
    {
        if (_password is not null)
        {
            Array.Clear(_password);
            _password = null;
        }

        if (_importBackup is not null)
        {
            CryptographicOperations.ZeroMemory(_importBackup);
            _importBackup = null;
        }
    }
}
