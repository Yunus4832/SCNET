using System.Xml.Linq;

using Game.Modding;

namespace Game.Screens;

public class ModManagementScreen : Screen
{
    private readonly ButtonWidget _addWorldModButton;
    private readonly ButtonWidget _globalModButton;
    private readonly ListPanelWidget _modsList;
    private readonly ButtonWidget _nextPageButton;
    private readonly ButtonWidget _previousPageButton;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _removeWorldModButton;
    private readonly TextBoxWidget _repositoryTextBox;
    private readonly ButtonWidget _saveDefaultRepositoryButton;

    private readonly List<RepositoryModItem> _items = [];
    private ModProfile _globalProfile = new();

    public ModManagementScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ModManagementScreen");
        LoadContents(this, node);
        _addWorldModButton = Children.Find<ButtonWidget>("AddWorldModButton")!;
        _globalModButton = Children.Find<ButtonWidget>("GlobalModButton")!;
        _modsList = Children.Find<ListPanelWidget>("RepositoryModsList")!;
        _nextPageButton = Children.Find<ButtonWidget>("NextPageButton")!;
        _previousPageButton = Children.Find<ButtonWidget>("PreviousPageButton")!;
        _refreshButton = Children.Find<ButtonWidget>("RefreshButton")!;
        _removeWorldModButton = Children.Find<ButtonWidget>("RemoveWorldModButton")!;
        _repositoryTextBox = Children.Find<TextBoxWidget>("RepositoryTextBox")!;
        _saveDefaultRepositoryButton = Children.Find<ButtonWidget>("SaveDefaultRepositoryButton")!;
        _modsList.ItemWidgetFactory = CreateModItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        WorldsManager.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        _repositoryTextBox.Text = GetRepositoryUrl();
        RefreshState();
        if (_items.Count == 0)
        {
            RefreshRepositoryPackages();
        }
    }

    public override void Update()
    {
        var selectedItem = _modsList.SelectedItem as RepositoryModItem;
        _globalModButton.IsEnabled = selectedItem != null;
        _globalModButton.Text = selectedItem is { IsGlobal: true } ? "移出全局" : "加入全局";
        _addWorldModButton.IsEnabled = selectedItem != null;
        _removeWorldModButton.IsEnabled = selectedItem != null && selectedItem.IsAnyWorld;
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

        if (_globalModButton.IsClicked && selectedItem != null)
        {
            if (selectedItem.IsGlobal)
            {
                RemovePackage(_globalProfile, selectedItem.Package.ModId);
            }
            else
            {
                AddPackage(_globalProfile, selectedItem.Package);
            }

            SaveGlobalProfile();
        }

        if (_addWorldModButton.IsClicked && selectedItem != null)
        {
            SelectWorldForPackage(selectedItem.Package, add: true);
        }

        if (_removeWorldModButton.IsClicked && selectedItem != null)
        {
            SelectWorldForPackage(selectedItem.Package, add: false);
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
            DialogsManager.Alert("模组服务器地址为空", "请先输入模组服务器地址。");
            return;
        }

        var busyDialog = new BusyDialog("正在读取模组服务器", repositoryUrl);
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
                    _items.Clear();
                    _items.AddRange(packages
                        .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
                        .Select(package => new RepositoryModItem(package)));
                    RefreshState();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert("读取模组服务器失败", ex.Message);
                });
            }
        });
    }

    private void SaveGlobalProfile()
    {
        ModProfileManager.SaveGlobalProfile(_globalProfile);
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        RefreshState();
    }

    private void SaveRepositoryUrlFromTextBox()
    {
        SettingsManager.Current.DefaultModRepositoryUrl = NormalizeRepositoryUrl(_repositoryTextBox.Text);
        SettingsManager.SaveSettings();
    }

    private void SelectWorldForPackage(ModRepositoryPackage package, bool add)
    {
        WorldsManager.UpdateWorldsList();
        DialogsManager.ShowDialog(null, new ListSelectionDialog(add ? "加入到世界" : "从世界移除",
            WorldsManager.WorldInfos, 70f,
            item => ((WorldInfo)item).WorldSettings.Name,
            item =>
            {
                var world = (WorldInfo)item;
                var profile = ModProfileManager.LoadWorldProfile(world.DirectoryName) ?? new ModProfile();
                if (add)
                {
                    AddPackage(profile, package);
                }
                else
                {
                    RemovePackage(profile, package.ModId);
                }

                ModProfileManager.SaveWorldProfile(world.DirectoryName, profile);
                RefreshState();
            }));
    }

    private static void AddPackage(ModProfile profile, ModRepositoryPackage package)
    {
        RemovePackage(profile, package.ModId);
        profile.Packages.Add(new ModPackageRequirement
        {
            ModId = package.ModId,
            Version = package.Version
        });
    }

    private static void RemovePackage(ModProfile profile, string modId)
    {
        profile.Packages.RemoveAll(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshState()
    {
        var selectedPackage = (_modsList.SelectedItem as RepositoryModItem)?.Package;
        var anyWorldModIds = LoadAnyWorldModIds();
        foreach (var item in _items)
        {
            item.IsGlobal = ContainsMod(_globalProfile, item.Package.ModId);
            item.IsAnyWorld = anyWorldModIds.Contains(item.Package.ModId);
        }

        _modsList.ClearItems();
        foreach (var item in _items)
        {
            _modsList.AddItem(item);
            if (selectedPackage != null &&
                string.Equals(item.Package.ModId, selectedPackage.ModId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Package.Version, selectedPackage.Version, StringComparison.OrdinalIgnoreCase))
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

    private static string GetRepositoryUrl()
    {
        return NormalizeRepositoryUrl(SettingsManager.Current.DefaultModRepositoryUrl);
    }

    private static string NormalizeRepositoryUrl(string? repositoryUrl)
    {
        return string.IsNullOrWhiteSpace(repositoryUrl) ? string.Empty : repositoryUrl.Trim();
    }

    private static Widget CreateModItemWidget(object item)
    {
        var mod = (RepositoryModItem)item;
        var package = mod.Package;
        var status = new List<string>();
        if (mod.IsGlobal)
        {
            status.Add("全局");
        }

        if (mod.IsAnyWorld)
        {
            status.Add("世界");
        }

        var details = $"{package.Version} | {package.Side}";
        if (status.Count > 0)
        {
            details += $" | {string.Join(" / ", status)}";
        }

        if (!string.IsNullOrWhiteSpace(package.Description))
        {
            details += $" | {package.Description}";
        }

        return new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Children =
            {
                new LabelWidget
                {
                    Text = package.ModId,
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

    private sealed class RepositoryModItem(ModRepositoryPackage package)
    {
        public ModRepositoryPackage Package { get; } = package;

        public bool IsGlobal { get; set; }

        public bool IsAnyWorld { get; set; }
    }
}
