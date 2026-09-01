using System.Xml.Linq;

using Content.Packaging;

using Game.Content;

namespace Game.Screens;

public class ModManagementScreen : Screen
{
    private const string _typeName = nameof(ModManagementScreen);

    private readonly ButtonWidget _addWorldModButton;
    private readonly ButtonWidget _cacheButton;
    private readonly ButtonWidget _exportButton;
    private readonly ButtonWidget _globalModButton;
    private readonly ButtonWidget _importButton;
    private readonly ListPanelWidget _modsList;
    private readonly ButtonWidget _nextPageButton;
    private readonly ButtonWidget _previousPageButton;
    private readonly LabelWidget _pickerUnavailableLabel;
    private readonly ButtonWidget _refreshButton;
    private readonly TextBoxWidget _contentServerTextBox;
    private readonly ButtonWidget _saveDefaultContentServerButton;

    private readonly List<ModItem> _items = [];
    private ModProfile _globalProfile = new();

    public ModManagementScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ModManagementScreen");
        LoadContents(this, node);
        _addWorldModButton = Children.Find<ButtonWidget>("AddWorldModButton")!;
        _cacheButton = Children.Find<ButtonWidget>("CacheButton")!;
        _exportButton = Children.Find<ButtonWidget>("ExportButton")!;
        _globalModButton = Children.Find<ButtonWidget>("GlobalModButton")!;
        _importButton = Children.Find<ButtonWidget>("ImportButton")!;
        _modsList = Children.Find<ListPanelWidget>("ModsList")!;
        _nextPageButton = Children.Find<ButtonWidget>("NextPageButton")!;
        _previousPageButton = Children.Find<ButtonWidget>("PreviousPageButton")!;
        _pickerUnavailableLabel = Children.Find<LabelWidget>("PickerUnavailable")!;
        _refreshButton = Children.Find<ButtonWidget>("RefreshButton")!;
        _contentServerTextBox = Children.Find<TextBoxWidget>("ContentServerTextBox")!;
        _saveDefaultContentServerButton = Children.Find<ButtonWidget>("SaveDefaultContentServerButton")!;
        _modsList.ItemWidgetFactory = CreateModItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        WorldsManager.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        _contentServerTextBox.Text = GetContentServerUrl();
        LoadLocalPackages();
        RefreshState();
        if (_items.Count == 0 && !string.IsNullOrWhiteSpace(_contentServerTextBox.Text))
        {
            RefreshContentServerPackages();
        }
    }

    public override void Update()
    {
        var selectedItem = _modsList.SelectedItem as ModItem;
        _globalModButton.IsEnabled = selectedItem != null;
        _globalModButton.Text = selectedItem is { IsGlobal: true }
            ? LanguageManager.Get(_typeName, "RemoveGlobal")
            : LanguageManager.Get(_typeName, "AddGlobal");
        _addWorldModButton.IsEnabled = selectedItem != null;
        _cacheButton.IsEnabled = selectedItem is { LocalEntry: not null } or { RemotePackage: not null };
        _cacheButton.Text = selectedItem?.LocalEntry != null
            ? LanguageManager.Get(_typeName, "DeleteCacheShort")
            : LanguageManager.Get(_typeName, "Download");
        _exportButton.IsEnabled = FilePicker.IsAvailable && selectedItem?.LocalEntry != null;
        _importButton.IsEnabled = FilePicker.IsAvailable;
        _pickerUnavailableLabel.IsVisible = !FilePicker.IsAvailable;
        _previousPageButton.IsEnabled = false;
        _nextPageButton.IsEnabled = false;

        if (_refreshButton.IsClicked)
        {
            RefreshContentServerPackages();
        }

        if (_importButton.IsClicked)
        {
            ImportPackages();
        }

        if (_saveDefaultContentServerButton.IsClicked)
        {
            SaveContentServerUrlFromTextBox();
        }

        if (_cacheButton.IsClicked && selectedItem != null)
        {
            if (selectedItem.LocalEntry != null)
            {
                ConfirmDeleteCache(selectedItem);
            }
            else if (selectedItem.RemotePackage != null)
            {
                DownloadPackage(selectedItem);
            }
        }

        if (_exportButton.IsClicked && selectedItem?.LocalEntry != null)
        {
            ExportPackage(selectedItem);
        }

        if (_globalModButton.IsClicked && selectedItem != null)
        {
            if (selectedItem.IsGlobal)
            {
                RemovePackage(_globalProfile, selectedItem.ModId);
            }
            else
            {
                AddPackage(_globalProfile, selectedItem);
            }

            SaveGlobalProfile();
        }

        if (_addWorldModButton.IsClicked && selectedItem != null)
        {
            SelectWorldsForPackage(selectedItem);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void RefreshContentServerPackages()
    {
        var contentServerUrl = NormalizeContentServerUrl(_contentServerTextBox.Text);
        if (string.IsNullOrWhiteSpace(contentServerUrl))
        {
            DialogsManager.Alert(
                LanguageManager.Get(_typeName, "ContentServerEmptyTitle"),
                LanguageManager.Get(_typeName, "ContentServerEmptyMessage"));
            return;
        }

        var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "ReadingContentServer"), contentServerUrl);
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                using var client = new ContentServerClient(contentServerUrl);
                var packages = client.ListMods();
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    foreach (var item in _items)
                    {
                        item.ClearRemote();
                    }

                    foreach (var package in packages)
                    {
                        UpsertItem(new ModItem(package, contentServerUrl));
                    }

                    _items.RemoveAll(item => item.LocalEntry == null && item.RemotePackage == null);
                    RefreshState();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ReadContentServerFailed"), ex.Message);
                });
            }
        });
    }

    private void LoadLocalPackages()
    {
        var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        foreach (var entry in repository.ListAll())
        {
            UpsertItem(new ModItem(entry));
        }
    }

    private void UpsertItem(ModItem newItem)
    {
        var existing = _items.FirstOrDefault(item =>
            string.Equals(item.ModId, newItem.ModId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Version, newItem.Version, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            _items.Add(newItem);
            return;
        }

        existing.Merge(newItem);
    }

    private void SaveGlobalProfile()
    {
        ModProfileManager.SaveGlobalProfile(_globalProfile);
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        RefreshState();
    }

    private void SaveContentServerUrlFromTextBox()
    {
        SettingsManager.Current.ContentServerUrl = NormalizeContentServerUrl(_contentServerTextBox.Text);
        SettingsManager.SaveSettings();
    }

    private void DownloadPackage(ModItem mod)
    {
        if (mod.RemotePackage == null || string.IsNullOrWhiteSpace(mod.ContentServerUrl))
        {
            return;
        }

        var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "DownloadingMod"), $"{mod.ModId}@{mod.Version}");
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                using var client = new ContentServerClient(mod.ContentServerUrl);
                var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
                var entry = client.DownloadMod(mod.RemotePackage, repository);
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    mod.LocalEntry = entry;
                    RefreshState();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "DownloadModFailed"), ex.Message);
                });
            }
        });
    }

    private void ConfirmDeleteCache(ModItem mod)
    {
        var message = string.Format(LanguageManager.Get(_typeName, "DeleteCacheQuestion"), mod.ModId, mod.Version);
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "DeleteCacheTitle"),
            message,
            LanguageManager.Yes,
            LanguageManager.No,
            button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    DeleteCache(mod);
                }
            }));
    }

    private void DeleteCache(ModItem mod)
    {
        if (mod.LocalEntry == null)
        {
            return;
        }

        var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
        repository.DeletePackage(mod.LocalEntry);
        mod.LocalEntry = null;
        if (mod.RemotePackage == null)
        {
            _items.Remove(mod);
        }

        RefreshState();
    }

    private async void ImportPackages()
    {
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest([ContentPackageReader.FileExtension],
                AllowMultiple: true, Title: LanguageManager.Get(_typeName, "SelectPackages")));
            if (files.Count == 0) return;
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
                LoadLocalPackages();
                RefreshState();
                DialogsManager.Alert(string.Format(LanguageManager.Get(_typeName, "ImportComplete"), imported));
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Dispatch(() => DialogsManager.Alert(LanguageManager.Get(_typeName, "ImportModFailed"), ex.Message));
        }
    }

    private async void ExportPackage(ModItem mod)
    {
        if (mod.LocalEntry == null)
        {
            return;
        }

        try
        {
            var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest(
                $"{mod.ModId}-{mod.Version}{ContentPackageReader.FileExtension}",
                "application/vnd.scnet.content-package", LanguageManager.Get(_typeName, "ExportTitle")));
            if (target is null) return;
            var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
            await using var destination = await target.OpenWriteAsync(CancellationToken.None);
            repository.ExportPackage(mod.LocalEntry, destination);
            Dispatcher.Dispatch(() => DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportComplete"), target.Name));
        }
        catch (Exception ex)
        {
            Dispatcher.Dispatch(() => DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportModFailed"), ex.Message));
        }
    }

    private void SelectWorldsForPackage(ModItem mod)
    {
        WorldsManager.UpdateWorldsList();
        if (WorldsManager.WorldInfos.Count == 0)
        {
            DialogsManager.Alert(
                LanguageManager.Get(_typeName, "NoWorldTitle"),
                LanguageManager.Get(_typeName, "NoWorldMessage"));
            return;
        }

        DialogsManager.ShowDialog(
            null,
            new ModWorldSelectionDialog(
                $"{mod.ModId}@{mod.Version}",
                WorldsManager.WorldInfos,
                world =>
                {
                    var profile = ModProfileManager.LoadWorldProfile(world.DirectoryName);
                    return profile != null && ContainsMod(profile, mod.ModId);
                },
                selections =>
                {
                    foreach (var selection in selections)
                    {
                        var profile = ModProfileManager.LoadWorldProfile(selection.World.DirectoryName) ??
                                      new ModProfile();
                        if (selection.IsChecked)
                        {
                            AddPackage(profile, mod);
                        }
                        else
                        {
                            RemovePackage(profile, mod.ModId);
                        }

                        ModProfileManager.SaveWorldProfile(selection.World.DirectoryName, profile);
                    }

                    RefreshState();
                }));
    }

    private static void AddPackage(ModProfile profile, ModItem mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.ContentServerUrl))
        {
            profile.ContentServerUrl = mod.ContentServerUrl;
        }

        RemovePackage(profile, mod.ModId);
        profile.Packages.Add(new ModPackageRequirement
        {
            ModId = mod.ModId,
            Version = mod.Version
        });
    }

    private static void RemovePackage(ModProfile profile, string modId)
    {
        profile.Packages.RemoveAll(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshState()
    {
        var selectedItem = _modsList.SelectedItem as ModItem;
        var anyWorldModIds = LoadAnyWorldModIds();
        foreach (var item in _items)
        {
            item.IsGlobal = ContainsMod(_globalProfile, item.ModId);
            item.IsAnyWorld = anyWorldModIds.Contains(item.ModId);
        }

        _modsList.ClearItems();
        foreach (var item in _items
                     .OrderBy(item => item.ModId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase))
        {
            _modsList.AddItem(item);
            if (selectedItem != null &&
                string.Equals(item.ModId, selectedItem.ModId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Version, selectedItem.Version, StringComparison.OrdinalIgnoreCase))
            {
                _modsList.SelectedItem = item;
            }
        }
    }

    private static HashSet<string> LoadAnyWorldModIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in WorldsManager.WorldInfos)
        {
            var profile = ModProfileManager.LoadWorldProfile(world.DirectoryName);
            if (profile == null)
            {
                continue;
            }

            foreach (var package in profile.Packages)
            {
                result.Add(package.ModId);
            }
        }

        return result;
    }

    private static bool ContainsMod(ModProfile profile, string modId)
    {
        return profile.Packages.Any(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetContentServerUrl()
    {
        return NormalizeContentServerUrl(SettingsManager.Current.ContentServerUrl);
    }

    private static string NormalizeContentServerUrl(string? contentServerUrl)
    {
        return string.IsNullOrWhiteSpace(contentServerUrl) ? string.Empty : contentServerUrl.Trim();
    }

    private static Widget CreateModItemWidget(object item)
    {
        var mod = (ModItem)item;
        var status = new List<string>();
        if (mod.LocalEntry != null)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusCached"));
        }

        if (mod.RemotePackage != null)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusRemote"));
        }

        if (mod.HasHashMismatch)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusHashMismatch"));
        }

        if (mod.IsGlobal)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusGlobal"));
        }

        if (mod.IsAnyWorld)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusWorld"));
        }

        var side = mod.RemotePackage?.Side ?? LanguageManager.Get(_typeName, "SideLocal");
        var details = $"{mod.Version} | {side}";
        if (status.Count > 0)
        {
            details += $" | {string.Join(" / ", status)}";
        }

        if (!string.IsNullOrWhiteSpace(mod.RemotePackage?.Description))
        {
            details += $" | {mod.RemotePackage.Description}";
        }

        return new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Children =
            {
                new LabelWidget
                {
                    Text = mod.ModId,
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center
                },
                new LabelWidget
                {
                    Text = details,
                    Color = Color.Gray,
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center,
                    WordWrap = true
                }
            }
        };
    }

    private sealed class ModItem
    {
        public ModItem(ContentServerModPackage package, string contentServerUrl)
        {
            ModId = package.ModId;
            Version = package.Version;
            RemotePackage = package;
            ContentServerUrl = contentServerUrl;
        }

        public ModItem(LocalModPackageEntry localEntry)
        {
            ModId = localEntry.ModId;
            Version = localEntry.Version;
            LocalEntry = localEntry;
        }

        public string ModId { get; }

        public string Version { get; }

        public ContentServerModPackage? RemotePackage { get; private set; }

        public LocalModPackageEntry? LocalEntry { get; set; }

        public string ContentServerUrl { get; private set; } = string.Empty;

        public bool HasHashMismatch =>
            LocalEntry != null &&
            RemotePackage != null &&
            !string.IsNullOrWhiteSpace(RemotePackage.PackageHash) &&
            !string.Equals(LocalEntry.PackageHash, RemotePackage.PackageHash, StringComparison.OrdinalIgnoreCase);

        public bool IsGlobal { get; set; }

        public bool IsAnyWorld { get; set; }

        public void Merge(ModItem other)
        {
            if (other.RemotePackage != null)
            {
                RemotePackage = other.RemotePackage;
                ContentServerUrl = other.ContentServerUrl;
            }

            if (other.LocalEntry != null)
            {
                LocalEntry = other.LocalEntry;
            }
        }

        public void ClearRemote()
        {
            RemotePackage = null;
            ContentServerUrl = string.Empty;
        }
    }
}
