using System.Xml.Linq;

namespace Game.Screens;

public class ModManagementScreen : Screen
{
    private const string _typeName = nameof(ModManagementScreen);

    private readonly ButtonWidget _addWorldModButton;
    private readonly ButtonWidget _cacheButton;
    private readonly ButtonWidget _exportButton;
    private readonly ButtonWidget _globalModButton;
    private readonly ListPanelWidget _modsList;
    private readonly ButtonWidget _nextPageButton;
    private readonly ButtonWidget _previousPageButton;
    private readonly ButtonWidget _refreshButton;
    private readonly TextBoxWidget _repositoryTextBox;
    private readonly ButtonWidget _saveDefaultRepositoryButton;

    private readonly List<RepositoryModItem> _items = [];
    private ModProfile _globalProfile = new();

    public ModManagementScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ModManagementScreen");
        LoadContents(this, node);
        _addWorldModButton = Children.Find<ButtonWidget>("AddWorldModButton")!;
        _cacheButton = Children.Find<ButtonWidget>("CacheButton")!;
        _exportButton = Children.Find<ButtonWidget>("ExportButton")!;
        _globalModButton = Children.Find<ButtonWidget>("GlobalModButton")!;
        _modsList = Children.Find<ListPanelWidget>("RepositoryModsList")!;
        _nextPageButton = Children.Find<ButtonWidget>("NextPageButton")!;
        _previousPageButton = Children.Find<ButtonWidget>("PreviousPageButton")!;
        _refreshButton = Children.Find<ButtonWidget>("RefreshButton")!;
        _repositoryTextBox = Children.Find<TextBoxWidget>("RepositoryTextBox")!;
        _saveDefaultRepositoryButton = Children.Find<ButtonWidget>("SaveDefaultRepositoryButton")!;
        _modsList.ItemWidgetFactory = CreateModItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        WorldsManager.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        _repositoryTextBox.Text = GetRepositoryUrl();
        LoadLocalPackages();
        RefreshState();
        if (_items.Count == 0 && !string.IsNullOrWhiteSpace(_repositoryTextBox.Text))
        {
            RefreshRepositoryPackages();
        }
    }

    public override void Update()
    {
        var selectedItem = _modsList.SelectedItem as RepositoryModItem;
        _globalModButton.IsEnabled = selectedItem != null;
        _globalModButton.Text = selectedItem is { IsGlobal: true }
            ? LanguageManager.Get(_typeName, "RemoveGlobal")
            : LanguageManager.Get(_typeName, "AddGlobal");
        _addWorldModButton.IsEnabled = selectedItem != null;
        _cacheButton.IsEnabled = selectedItem is { LocalEntry: not null } or { RemotePackage: not null };
        _cacheButton.Text = selectedItem?.LocalEntry != null
            ? LanguageManager.Get(_typeName, "DeleteCacheShort")
            : LanguageManager.Get(_typeName, "Download");
        _exportButton.IsEnabled = selectedItem?.LocalEntry != null;
        _previousPageButton.IsEnabled = false;
        _nextPageButton.IsEnabled = false;

        if (_refreshButton.IsClicked)
        {
            RefreshRepositoryPackages();
        }

        if (_saveDefaultRepositoryButton.IsClicked)
        {
            SaveRepositoryUrlFromTextBox();
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

    private void RefreshRepositoryPackages()
    {
        var repositoryUrl = NormalizeRepositoryUrl(_repositoryTextBox.Text);
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            DialogsManager.Alert(
                LanguageManager.Get(_typeName, "RepositoryEmptyTitle"),
                LanguageManager.Get(_typeName, "RepositoryEmptyMessage"));
            return;
        }

        var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "ReadingRepository"), repositoryUrl);
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                using var client = new ModServerClient(repositoryUrl);
                var packages = client.ListPackages();
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    foreach (var item in _items)
                    {
                        item.ClearRemote();
                    }

                    foreach (var package in packages)
                    {
                        UpsertItem(new RepositoryModItem(package, repositoryUrl));
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
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ReadRepositoryFailed"), ex.Message);
                });
            }
        });
    }

    private void LoadLocalPackages()
    {
        var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ModCache));
        foreach (var entry in repository.ListAll())
        {
            UpsertItem(new RepositoryModItem(entry));
        }
    }

    private void UpsertItem(RepositoryModItem newItem)
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

    private void SaveRepositoryUrlFromTextBox()
    {
        SettingsManager.Current.ContentServerUrl = NormalizeRepositoryUrl(_repositoryTextBox.Text);
        SettingsManager.SaveSettings();
    }

    private void DownloadPackage(RepositoryModItem mod)
    {
        if (mod.RemotePackage == null || string.IsNullOrWhiteSpace(mod.RepositoryUrl))
        {
            return;
        }

        var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "DownloadingMod"), $"{mod.ModId}@{mod.Version}");
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                using var client = new ModServerClient(mod.RepositoryUrl);
                var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ModCache));
                var entry = client.DownloadPackage(mod.RemotePackage, repository);
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

    private void ConfirmDeleteCache(RepositoryModItem mod)
    {
        var importedSources = FindImportedSources(mod);
        var message = importedSources.Count == 0
            ? string.Format(LanguageManager.Get(_typeName, "DeleteCacheQuestion"), mod.ModId, mod.Version)
            : string.Format(LanguageManager.Get(_typeName, "DeleteCacheImportedQuestion"), mod.ModId, mod.Version);
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

    private void DeleteCache(RepositoryModItem mod)
    {
        if (mod.LocalEntry == null)
        {
            return;
        }

        var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ModCache));
        repository.DeletePackage(mod.LocalEntry);
        mod.LocalEntry = null;
        if (mod.RemotePackage == null)
        {
            _items.Remove(mod);
        }

        RefreshState();
    }

    private void ExportPackage(RepositoryModItem mod)
    {
        if (mod.LocalEntry == null)
        {
            return;
        }

        try
        {
            var targetPath = LocalModsImportManager.ExportPackage(
                mod.LocalEntry,
                Storage.GetSystemPath(GamePaths.Mods));
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportComplete"), targetPath);
        }
        catch (Exception ex)
        {
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportModFailed"), ex.Message);
        }
    }

    private void SelectWorldsForPackage(RepositoryModItem mod)
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

    private static void AddPackage(ModProfile profile, RepositoryModItem mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.RepositoryUrl))
        {
            profile.RepositoryUrl = mod.RepositoryUrl;
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
        var selectedItem = _modsList.SelectedItem as RepositoryModItem;
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

    private static List<string> FindImportedSources(RepositoryModItem mod)
    {
        if (mod.LocalEntry == null)
        {
            return [];
        }

        return LocalModsImportManager.ListImportedMods()
            .Where(entry =>
                string.Equals(entry.PackageHash, mod.LocalEntry.PackageHash, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Path)
            .ToList();
    }

    private static string GetRepositoryUrl()
    {
        return NormalizeRepositoryUrl(SettingsManager.Current.ContentServerUrl);
    }

    private static string NormalizeRepositoryUrl(string? repositoryUrl)
    {
        return string.IsNullOrWhiteSpace(repositoryUrl) ? string.Empty : repositoryUrl.Trim();
    }

    private static Widget CreateModItemWidget(object item)
    {
        var mod = (RepositoryModItem)item;
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

    private sealed class RepositoryModItem
    {
        public RepositoryModItem(ModRepositoryPackage package, string repositoryUrl)
        {
            ModId = package.ModId;
            Version = package.Version;
            RemotePackage = package;
            RepositoryUrl = repositoryUrl;
        }

        public RepositoryModItem(LocalModPackageEntry localEntry)
        {
            ModId = localEntry.ModId;
            Version = localEntry.Version;
            LocalEntry = localEntry;
        }

        public string ModId { get; }

        public string Version { get; }

        public ModRepositoryPackage? RemotePackage { get; private set; }

        public LocalModPackageEntry? LocalEntry { get; set; }

        public string RepositoryUrl { get; private set; } = string.Empty;

        public bool HasHashMismatch =>
            LocalEntry != null &&
            RemotePackage != null &&
            !string.IsNullOrWhiteSpace(RemotePackage.PackageHash) &&
            !string.Equals(LocalEntry.PackageHash, RemotePackage.PackageHash, StringComparison.OrdinalIgnoreCase);

        public bool IsGlobal { get; set; }

        public bool IsAnyWorld { get; set; }

        public void Merge(RepositoryModItem other)
        {
            if (other.RemotePackage != null)
            {
                RemotePackage = other.RemotePackage;
                RepositoryUrl = other.RepositoryUrl;
            }

            if (other.LocalEntry != null)
            {
                LocalEntry = other.LocalEntry;
            }
        }

        public void ClearRemote()
        {
            RemotePackage = null;
            RepositoryUrl = string.Empty;
        }
    }
}
