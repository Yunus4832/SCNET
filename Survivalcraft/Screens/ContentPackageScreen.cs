using System.Xml.Linq;

using Content.Packaging;

using Game.Content;

namespace Game.Screens;

public sealed class ContentPackageScreen : Screen
{
    private const string _typeName = nameof(ContentPackageScreen);

    private static readonly ContentPackageType[] _allowedTypes =
    [
        ContentPackageType.World,
        ContentPackageType.BlocksTexture,
        ContentPackageType.CharacterSkin,
        ContentPackageType.FurniturePack
    ];

    private readonly ListPanelWidget _packageList;
    private readonly ButtonWidget _importButton;
    private readonly ButtonWidget _exportButton;
    private readonly ButtonWidget _installButton;
    private readonly ButtonWidget _deleteButton;
    private readonly ButtonWidget _createButton;
    private readonly LabelWidget _pickerUnavailableLabel;
    private bool _busy;
    private int _operationGeneration;

    public ContentPackageScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentPackageScreen");
        LoadContents(this, node);
        _packageList = Children.Find<ListPanelWidget>("PackageList")!;
        _importButton = Children.Find<ButtonWidget>("Import")!;
        _exportButton = Children.Find<ButtonWidget>("Export")!;
        _installButton = Children.Find<ButtonWidget>("Install")!;
        _deleteButton = Children.Find<ButtonWidget>("Delete")!;
        _createButton = Children.Find<ButtonWidget>("Create")!;
        _pickerUnavailableLabel = Children.Find<LabelWidget>("PickerUnavailable")!;
        _packageList.ItemWidgetFactory = item =>
        {
            var package = (ContentPackageCacheEntry)item;
            var widget = (ContainerWidget)LoadWidget(
                this, ContentManager.Get<XElement>("Widgets/ContentServerItem"), null);
            widget.Children.Find<LabelWidget>("ContentServerItem.Name")!.Text =
                $"{package.Name}  {package.Version}";
            widget.Children.Find<LabelWidget>("ContentServerItem.Details")!.Text =
                $"{package.Type} | {package.Identifier} | {DataSizeFormatter.Format(package.Size)}";
            return widget;
        };
    }

    public override void Enter(object[] parameters)
    {
        _operationGeneration++;
        Refresh();
    }

    public override void Leave()
    {
        _operationGeneration++;
        _busy = false;
        _packageList.SelectedItem = null;
    }

    public override void Update()
    {
        var selected = _packageList.SelectedItem as ContentPackageCacheEntry;
        var pickerAvailable = FilePicker.IsAvailable;
        _pickerUnavailableLabel.IsVisible = !pickerAvailable;
        _importButton.IsEnabled = !_busy && pickerAvailable;
        _createButton.IsEnabled = !_busy && pickerAvailable;
        _exportButton.IsEnabled = !_busy && pickerAvailable && selected is not null;
        _installButton.IsEnabled = !_busy && selected is not null;
        _deleteButton.IsEnabled = !_busy && selected is not null;

        if (_importButton.IsClicked)
        {
            ImportPackages();
        }

        if (_createButton.IsClicked)
        {
            var generation = _operationGeneration;
            ContentPackageCreationDialogs.Show(busy => SetBusy(generation, busy),
                () =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.Alert(LanguageManager.Get(_typeName, "CreationSaved"));
                    }
                }, exception => ShowError(generation, exception));
        }

        if (_exportButton.IsClicked && selected is not null)
        {
            ExportPackage(selected);
        }

        if (_installButton.IsClicked && selected is not null)
        {
            var generation = _operationGeneration;
            ContentPackageInstallDialogs.Show(selected, busy => SetBusy(generation, busy),
                () =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.Alert(LanguageManager.Get(_typeName, "Installed"));
                    }
                }, exception => ShowError(generation, exception));
        }

        if (_deleteButton.IsClicked && selected is not null)
        {
            ConfirmDelete(selected);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void Refresh()
    {
        var selectedHash = (_packageList.SelectedItem as ContentPackageCacheEntry)?.PackageHash;
        var cache = CreateCache();
        cache.RebuildIndex();
        _packageList.ClearItems();
        foreach (var package in cache.List().Where(entry => entry.Type != ContentPackageType.Mod)
                     .OrderBy(entry => entry.Type).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            _packageList.AddItem(package);
            if (string.Equals(package.PackageHash, selectedHash, StringComparison.OrdinalIgnoreCase))
            {
                _packageList.SelectedItem = package;
            }
        }
    }

    private async void ImportPackages()
    {
        var generation = _operationGeneration;
        _busy = true;
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest(
                [ContentPackageReader.FileExtension], true, LanguageManager.Get(_typeName, "SelectPackages")));
            if (files.Count == 0)
            {
                return;
            }

            if (!IsCurrentOperation(generation))
            {
                return;
            }

            var cache = CreateCache();
            var imported = 0;
            foreach (var file in files)
            {
                await using var source = await file.OpenReadAsync(CancellationToken.None);
                await cache.ImportAllowedAsync(source, _allowedTypes);
                imported++;
            }

            Dispatcher.Dispatch(() =>
            {
                if (!IsCurrentOperation(generation))
                {
                    return;
                }

                Refresh();
                DialogsManager.Alert(string.Format(LanguageManager.Get(_typeName, "ImportComplete"), imported));
            });
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrentOperation(generation))
                {
                    Refresh();
                    ShowError(exception);
                }
            });
        }
        finally
        {
            SetBusy(generation, false);
        }
    }

    private async void ExportPackage(ContentPackageCacheEntry package)
    {
        var generation = _operationGeneration;
        _busy = true;
        try
        {
            var fileName = MakeFileName($"{package.Identifier}-{package.Version}") + ContentPackageReader.FileExtension;
            var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest(fileName,
                "application/vnd.scnet.content-package", LanguageManager.Get(_typeName, "ExportTitle")));
            if (target is null)
            {
                return;
            }

            if (!IsCurrentOperation(generation))
            {
                return;
            }

            await using var destination = await target.OpenWriteAsync(CancellationToken.None);
            await CreateCache().ExportAsync(package.PackageHash, destination);
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrentOperation(generation))
                {
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportComplete"), target.Name);
                }
            });
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() => ShowError(generation, exception));
        }
        finally
        {
            SetBusy(generation, false);
        }
    }

    private void ConfirmDelete(ContentPackageCacheEntry package)
    {
        var generation = _operationGeneration;
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "DeleteTitle"),
            string.Format(LanguageManager.Get(_typeName, "DeleteQuestion"), package.Name, package.Version),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"),
            button =>
            {
                if (button != MessageDialogButton.Button1 || !IsCurrentOperation(generation))
                {
                    return;
                }

                CreateCache().Delete(package.PackageHash);
                Refresh();
            }));
    }

    private static string MakeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private static ContentPackageCache CreateCache() =>
        new(Storage.GetSystemPath(GamePaths.ContentPackageCache));

    private bool IsCurrentOperation(int generation)
    {
        return generation == _operationGeneration && ReferenceEquals(ScreensManager.CurrentScreen, this);
    }

    private void SetBusy(int generation, bool busy)
    {
        if (generation == _operationGeneration)
        {
            _busy = busy;
        }
    }

    private void ShowError(int generation, Exception exception)
    {
        if (IsCurrentOperation(generation))
        {
            ShowError(exception);
        }
    }

    private static void ShowError(Exception exception)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get("Usual", "error"), exception.Message, LanguageManager.Get("Usual", "ok")));
    }
}
