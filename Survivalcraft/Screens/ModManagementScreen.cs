using System.Xml.Linq;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Game.Screens;

public sealed class ModManagementScreen : Screen
{
    private const string _typeName = nameof(ModManagementScreen);

    private readonly ButtonWidget _cacheButton;
    private readonly LabelWidget _emptyLabel;
    private readonly ButtonWidget _exportButton;
    private readonly ButtonWidget _findButton;
    private readonly ButtonWidget _globalModButton;
    private readonly ButtonWidget _importButton;
    private readonly ListPanelWidget _modsList;
    private readonly ButtonWidget _onlineButton;
    private readonly LabelWidget _pickerUnavailableLabel;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _worldModButton;
    private bool _busy;
    private ModProfile _globalProfile = new();

    public ModManagementScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ModManagementScreen"));
        _cacheButton = Children.Find<ButtonWidget>("CacheButton")!;
        _emptyLabel = Children.Find<LabelWidget>("Empty")!;
        _exportButton = Children.Find<ButtonWidget>("ExportButton")!;
        _findButton = Children.Find<ButtonWidget>("FindButton")!;
        _globalModButton = Children.Find<ButtonWidget>("GlobalModButton")!;
        _importButton = Children.Find<ButtonWidget>("ImportButton")!;
        _modsList = Children.Find<ListPanelWidget>("ModsList")!;
        _onlineButton = Children.Find<ButtonWidget>("OnlineButton")!;
        _pickerUnavailableLabel = Children.Find<LabelWidget>("PickerUnavailable")!;
        _refreshButton = Children.Find<ButtonWidget>("RefreshButton")!;
        _worldModButton = Children.Find<ButtonWidget>("WorldModButton")!;
        _modsList.ItemWidgetFactory = CreateModItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        RefreshState();
    }

    public override void Update()
    {
        var selected = _modsList.SelectedItem as ManagedModItem;
        var pickerAvailable = FilePicker.IsAvailable;
        _globalModButton.IsEnabled = !_busy && selected is not null;
        _globalModButton.Text = selected?.IsGlobal == true
            ? LanguageManager.Get(_typeName, "RemoveGlobal")
            : LanguageManager.Get(_typeName, "AddGlobal");
        _worldModButton.IsEnabled = !_busy && selected is not null;
        _cacheButton.IsEnabled = !_busy && selected?.LocalEntry is not null;
        _findButton.IsEnabled = !_busy && selected?.IsMissing == true;
        _exportButton.IsEnabled = !_busy && pickerAvailable && selected?.LocalEntry is not null;
        _importButton.IsEnabled = !_busy && pickerAvailable;
        _onlineButton.IsEnabled = !_busy;
        _refreshButton.IsEnabled = !_busy;
        _pickerUnavailableLabel.IsVisible = !pickerAvailable;

        if (_importButton.IsClicked)
        {
            ImportPackages();
        }

        if (_cacheButton.IsClicked && selected?.LocalEntry is not null)
        {
            ConfirmDeleteCache(selected);
        }

        if (_findButton.IsClicked && selected is not null)
        {
            OpenOnlineContent(selected);
        }

        if (_exportButton.IsClicked && selected?.LocalEntry is not null)
        {
            ExportPackage(selected);
        }

        if (_globalModButton.IsClicked && selected is not null)
        {
            if (selected.IsGlobal)
            {
                RemovePackage(_globalProfile, selected.ModId);
            }
            else
            {
                AddPackage(_globalProfile, selected);
            }

            ModProfileManager.SaveGlobalProfile(_globalProfile);
            RefreshState();
        }

        if (_worldModButton.IsClicked && selected is not null)
        {
            SelectWorldsForPackage(selected);
        }

        if (_onlineButton.IsClicked)
        {
            if (selected is null)
            {
                ScreensManager.SwitchScreen("OnlineContent");
            }
            else
            {
                OpenOnlineContent(selected);
            }
        }

        if (_refreshButton.IsClicked)
        {
            RefreshState();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void RefreshState()
    {
        WorldsManager.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        var worldProfiles = WorldsManager.WorldInfos
            .Select(world => ModProfileManager.LoadWorldProfile(world.DirectoryName))
            .OfType<ModProfile>()
            .ToArray();
        var repository = CreateRepository();
        var selected = _modsList.SelectedItem as ManagedModItem;
        var items = ModManagementCatalog.Build(repository.ListAll(), _globalProfile, worldProfiles,
            CurrentModRuntime.Value?.EffectiveProfile);
        _modsList.ClearItems();
        foreach (var item in items)
        {
            _modsList.AddItem(item);
            if (selected is not null && IsSamePackage(selected, item))
            {
                _modsList.SelectedItem = item;
            }
        }

        _emptyLabel.IsVisible = items.Count == 0;
    }

    private static void OpenOnlineContent(ManagedModItem item)
    {
        ScreensManager.SwitchScreen("OnlineContent", new OnlineContentNavigation(
            ContentPackageType.Mod, item.ModId, item.Version, item.PackageHash, "ModManagement"));
    }

    private void ConfirmDeleteCache(ManagedModItem item)
    {
        var message = string.Format(LanguageManager.Get(_typeName, "DeleteCacheQuestion"),
            item.ModId, item.Version);
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "DeleteCacheTitle"),
            message,
            LanguageManager.Yes,
            LanguageManager.No,
            button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    DeleteCache(item);
                }
            }));
    }

    private void DeleteCache(ManagedModItem item)
    {
        if (item.LocalEntry is null)
        {
            return;
        }

        try
        {
            CreateRepository().DeletePackage(item.LocalEntry);
            RefreshState();
        }
        catch (InvalidOperationException exception)
        {
            Log.Warning($"Referenced mod cache removal was rejected: {exception.Message}");
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ReferencedCannotDelete"));
        }
        catch (Exception exception)
        {
            Log.Error($"Mod cache removal failed: {exception}");
            DialogsManager.Alert(LanguageManager.Get(_typeName, "DeleteCacheFailed"));
        }
    }

    private async void ImportPackages()
    {
        _busy = true;
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest([ContentPackageReader.FileExtension],
                AllowMultiple: true, Title: LanguageManager.Get(_typeName, "SelectPackages")));
            if (files.Count == 0)
            {
                return;
            }

            var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "Importing"), string.Empty);
            Dispatcher.Dispatch(() => DialogsManager.ShowDialog(null, busyDialog));
            var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
            var imported = 0;
            try
            {
                foreach (var file in files)
                {
                    await using var source = await file.OpenReadAsync(CancellationToken.None);
                    await cache.ImportExpectedAsync(source, ContentPackageType.Mod);
                    imported++;
                }
            }
            finally
            {
                Dispatcher.Dispatch(() => DialogsManager.HideDialog(busyDialog));
            }

            Dispatcher.Dispatch(() =>
            {
                RefreshState();
                DialogsManager.Alert(string.Format(LanguageManager.Get(_typeName, "ImportComplete"), imported));
            });
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
                DialogsManager.Alert(LanguageManager.Get(_typeName, "ImportModFailed"), exception.Message));
        }
        finally
        {
            _busy = false;
        }
    }

    private async void ExportPackage(ManagedModItem item)
    {
        if (item.LocalEntry is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest(
                $"{item.ModId}-{item.Version}{ContentPackageReader.FileExtension}",
                "application/vnd.scnet.content-package", LanguageManager.Get(_typeName, "ExportTitle")));
            if (target is null)
            {
                return;
            }

            await using var destination = await target.OpenWriteAsync(CancellationToken.None);
            CreateRepository().ExportPackage(item.LocalEntry, destination);
            Dispatcher.Dispatch(() =>
                DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportComplete"), target.Name));
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
                DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportModFailed"), exception.Message));
        }
        finally
        {
            _busy = false;
        }
    }

    private void SelectWorldsForPackage(ManagedModItem item)
    {
        WorldsManager.UpdateWorldsList();
        if (WorldsManager.WorldInfos.Count == 0)
        {
            DialogsManager.Alert(
                LanguageManager.Get(_typeName, "NoWorldTitle"),
                LanguageManager.Get(_typeName, "NoWorldMessage"));
            return;
        }

        DialogsManager.ShowDialog(null, new ModWorldSelectionDialog(
            $"{item.ModId}@{item.Version}",
            WorldsManager.WorldInfos,
            world => ModProfileManager.LoadWorldProfile(world.DirectoryName) is { } profile &&
                     ModManagementCatalog.ContainsExact(profile, item),
            selections =>
            {
                foreach (var selection in selections)
                {
                    var profile = ModProfileManager.LoadWorldProfile(selection.World.DirectoryName) ??
                                  new ModProfile();
                    if (selection.IsChecked)
                    {
                        AddPackage(profile, item);
                    }
                    else if (ModManagementCatalog.ContainsExact(profile, item))
                    {
                        RemovePackage(profile, item.ModId);
                    }

                    ModProfileManager.SaveWorldProfile(selection.World.DirectoryName, profile);
                }

                RefreshState();
            }));
    }

    private static void AddPackage(ModProfile profile, ManagedModItem item)
    {
        RemovePackage(profile, item.ModId);
        profile.Packages.Add(item.ToRequirement());
    }

    private static void RemovePackage(ModProfile profile, string modId)
    {
        profile.Packages.RemoveAll(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    private static LocalModRepository CreateRepository()
    {
        return new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
    }

    private static bool IsSamePackage(ManagedModItem first, ManagedModItem second)
    {
        return string.Equals(first.ModId, second.ModId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.Version, second.Version, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.PackageHash, second.PackageHash, StringComparison.Ordinal);
    }

    private static Widget CreateModItemWidget(object item)
    {
        var mod = (ManagedModItem)item;
        var status = new List<string>();
        status.Add(LanguageManager.Get(_typeName, mod.IsMissing ? "StatusMissing" : "StatusCached"));
        if (mod.IsGlobal)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusGlobal"));
        }

        if (mod.IsWorld)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusWorld"));
        }

        if (mod.IsRuntime)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusRuntime"));
        }

        return new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Children =
            {
                new LabelWidget
                {
                    Text = $"{mod.ModId}  {mod.Version}",
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center
                },
                new LabelWidget
                {
                    Text = $"{mod.PackageHash} | {string.Join(" / ", status)}",
                    Color = Color.Gray,
                    FontScale = 0.55f,
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center
                }
            }
        };
    }
}
