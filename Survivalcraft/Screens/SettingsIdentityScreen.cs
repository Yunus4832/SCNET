using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.FileStorage;

using Game.Identity;

namespace Game.Screens;

public sealed class SettingsIdentityScreen : Screen
{
    private readonly SelectionDrawerWidget _purpose;

    private readonly LabelWidget _address;

    private readonly LabelWidget _hint;

    private readonly ButtonWidget _copy;

    private readonly ButtonWidget _export;

    private readonly ButtonWidget _import;

    private bool _busy;

    private int _generation;

    private byte[]? _importBackup;

    private char[]? _password;

    private IdentityPasswordDialog? _passwordDialog;

    public SettingsIdentityScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/SettingsIdentityScreen"));
        _purpose = Children.Find<SelectionDrawerWidget>("Purpose")!;
        _address = Children.Find<LabelWidget>("Address")!;
        _hint = Children.Find<LabelWidget>("Hint")!;
        _copy = Children.Find<ButtonWidget>("Copy")!;
        _export = Children.Find<ButtonWidget>("Export")!;
        _import = Children.Find<ButtonWidget>("Import")!;
        _purpose.ItemTextProvider = item => Text(item is IdentityPurpose.Player ? "Player" : "Server");
        _purpose.SetItems([IdentityPurpose.Player, IdentityPurpose.Server]);
        _purpose.SelectedItem = IdentityPurpose.Player;
    }

    private IdentityPurpose Purpose => (IdentityPurpose)_purpose.SelectedItem!;

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(SettingsIdentityScreen), key);

    public override void Enter(object[] parameters)
    {
        _generation++;
        Refresh();
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

        if (_copy.IsClicked)
        {
            ClipboardManager.ClipboardString = LocalIdentityManager.Current.GetAddress(Purpose) ?? string.Empty;
        }

        if (_export.IsClicked) { BeginExport(); }

        if (_import.IsClicked) { BeginImport(); }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Settings");
        }
    }

    private void Refresh()
    {
        var address = LocalIdentityManager.Current.GetAddress(Purpose);
        _address.Text = address ?? Text("NotCreated");
        _hint.Text = !LocalIdentityManager.CanImport ? Text("OfflineRequired") : Text("SecurityHint");
        _copy.IsEnabled = !_busy && address is not null;
        _export.IsEnabled = !_busy && address is not null && FilePicker.IsAvailable;
        _import.IsEnabled = !_busy && LocalIdentityManager.CanImport && FilePicker.IsAvailable;
        _purpose.IsEnabled = !_busy;
    }

    private bool IsCurrent(int generation) => generation == _generation && ScreensManager.CurrentScreen == this;

    private void BeginExport()
    {
        _busy = true;
        var generation = _generation;
        var purpose = Purpose;
        _passwordDialog = new IdentityPasswordDialog(
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

                Export(purpose, password, generation);
            }
        );
        DialogsManager.ShowDialog(this, _passwordDialog);
    }

    private async void Export(IdentityPurpose purpose, char[] password, int generation)
    {
        byte[]? backup = null;
        try
        {
            try { backup = await Task.Run(() => LocalIdentityManager.Current.ExportBackup(purpose, password)); }
            finally { Array.Clear(password); }

            if (!IsCurrent(generation)) { return; }

            var target = await PickExportTarget(purpose, generation);
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

    private Task<PickedSaveTarget?> PickExportTarget(IdentityPurpose purpose, int generation)
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
                            $"scnet-{purpose.ToString().ToLowerInvariant()}-identity.p8",
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
        _passwordDialog = new IdentityPasswordDialog(
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
                    var address = LocalIdentityManager.Current.InspectBackup(purpose, backup, password);
                    var expected = LocalIdentityManager.Current.GetAddress(purpose);
                    DialogsManager.Confirm(
                        string.Format(Text("ImportConfirm"), expected ?? Text("NotCreated"), address),
                        button =>
                        {
                            try
                            {
                                if (button != MessageDialogButton.Button1 || !IsCurrent(generation)) { return; }

                                LocalIdentityManager.ImportBackup(purpose, backup, password, expected);
                                DialogsManager.Alert(Text("ImportComplete"));
                            }
                            catch { ShowError(generation, "ImportFailed"); }
                            finally { FinishImport(generation); }
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
