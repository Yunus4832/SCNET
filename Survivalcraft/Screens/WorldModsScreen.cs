using System.Xml.Linq;

using Game.Application;

namespace Game.Screens;

/// <summary>编辑调用方拥有的世界模组草稿，只修改包选择，不保存配置或加载模组代码。</summary>
public sealed class WorldModsScreen : Screen
{
    private enum ModAction
    {
        Toggle = 0,

        Details = 1,

        Refresh = 2
    }

    private sealed record ModItem(ManagedModItem Package, bool? CanRemoveFromWorld, string? Error);

    private readonly ActionPanelWidget _actions;

    private readonly LabelWidget _emptyLabel;

    private readonly ListPanelWidget _modsList;

    private readonly LabelWidget _unavailableLabel;

    private ModProfile _draft = null!;

    private ModItem[] _items = [];

    private int _languageRevision;

    private ModProfile? _originalProfile;

    private string _returnScreen = string.Empty;

    public WorldModsScreen()
    {
        LoadContents(this, AssetManager.Get<XElement>("Screens/WorldModsScreen"));
        _actions = Children.Find<ActionPanelWidget>("Actions")!;
        _emptyLabel = Children.Find<LabelWidget>("Empty")!;
        _modsList = Children.Find<ListPanelWidget>("ModsList")!;
        _unavailableLabel = Children.Find<LabelWidget>("Unavailable")!;
        _modsList.ItemWidgetFactory = CreateModItemWidget;
        _actions.ItemTextProvider = GetActionText;
        _actions.ItemEnabledProvider = item => (ModAction)item switch
        {
            ModAction.Toggle => _modsList.SelectedItem is ModItem selected && CanToggle(selected),
            ModAction.Details => _modsList.SelectedItem is ModItem,
            ModAction.Refresh => true,
            _ => false
        };
        _actions.ItemColorProvider = item => (ModAction)item == ModAction.Toggle &&
                                             _modsList.SelectedItem is ModItem selected
            ? IsSelected(selected) ? new Color(150, 50, 35) : new Color(50, 150, 35)
            : null;
        _actions.ItemClicked += ExecuteAction;
        _actions.SetPrimaryItems([ModAction.Toggle, ModAction.Details, ModAction.Refresh], [1f, 1f, 1f, 0f]);
    }

    /// <summary>接收独立草稿、返回界面名及可为空的原始世界配置，保留调用方的对象所有权。</summary>
    public override void Enter(object[] parameters)
    {
        _draft = (ModProfile)parameters[0];
        _returnScreen = (string)parameters[1];
        _originalProfile = parameters.Length > 2 ? (ModProfile)parameters[2] : null;
        _modsList.ClearItems();
        _modsList.ScrollPosition = 0f;
        _modsList.ScrollSpeed = 0f;
        _actions.ShowPrimaryItems();
        RefreshPackages();
    }

    public override void Leave()
    {
        _modsList.ClearItems();
        _items = [];
        _originalProfile = null;
        _draft = null!;
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_returnScreen);
            return;
        }

        if (_languageRevision != Localization.ClientLanguage.Revision)
        {
            RefreshList(_modsList.SelectedItem as ModItem);
        }

        _actions.Refresh();
    }

    private static string Text(string key) =>
        Localization.ClientLanguage.Catalog.GetContentWidgets(nameof(WorldModsScreen), key);

    private bool IsSelected(ModItem item) => ModManagementCatalog.ContainsExact(_draft, item.Package);

    private bool CanToggle(ModItem item)
    {
        return IsSelected(item)
            ? GetRemovalBlock(item) is null
            : !item.Package.IsMissing && item.Error is null;
    }

    private string? GetRemovalBlock(ModItem item)
    {
        if (_originalProfile is null)
        {
            return null;
        }

        // 替换版本不等于移除模组；真正移除时仍须使用原始精确包的声明，避免替换后绕过限制。
        var original = _items.FirstOrDefault(candidate =>
            string.Equals(candidate.Package.ModId, item.Package.ModId, StringComparison.OrdinalIgnoreCase) &&
            ModManagementCatalog.ContainsExact(_originalProfile, candidate.Package)
        );
        return original is null || original.CanRemoveFromWorld == true
            ? null
            : original.CanRemoveFromWorld == false
                ? "RemovalRequired"
                : "RemovalUnavailable";
    }

    private void TogglePackage(ModItem item)
    {
        if (!CanToggle(item))
        {
            return;
        }

        var selected = IsSelected(item);
        _draft.Packages.RemoveAll(package =>
            string.Equals(package.ModId, item.Package.ModId, StringComparison.OrdinalIgnoreCase)
        );
        if (!selected)
        {
            _draft.Packages.Add(item.Package.ToRequirement());
        }

        RefreshList(item);
    }

    private string GetActionText(object item)
    {
        if ((ModAction)item != ModAction.Toggle)
        {
            return Text(item.ToString()!);
        }

        if (_modsList.SelectedItem is not ModItem selected)
        {
            return Text("Select");
        }

        return Text(
            IsSelected(selected)
                ? "Remove"
                : _draft.Packages.Any(package =>
                    string.Equals(package.ModId, selected.Package.ModId, StringComparison.OrdinalIgnoreCase)
                )
                    ? "Replace"
                    : "Select"
        );
    }

    private void ExecuteAction(object item)
    {
        switch ((ModAction)item)
        {
            case ModAction.Toggle when _modsList.SelectedItem is ModItem selected:
                TogglePackage(selected);
                break;
            case ModAction.Details when _modsList.SelectedItem is ModItem selected:
                DialogsManager.Alert(
                    selected.Package.LocalEntry?.Name ?? selected.Package.ModId,
                    string.Join(
                        "\n\n",
                        $"{selected.Package.ModId}@{selected.Package.Version}\n{selected.Package.PackageHash}",
                        GetStatus(selected),
                        GetRemovalBlock(selected) is { } reason ? Text(reason) : string.Empty,
                        selected.Error ?? selected.Package.LocalEntry?.Description ?? string.Empty
                    )
                );
                break;
            case ModAction.Refresh:
                RefreshPackages();
                break;
        }
    }

    private void RefreshPackages()
    {
        var selected = _modsList.SelectedItem as ModItem;
        IReadOnlyList<LocalModPackageEntry> cached;
        _unavailableLabel.IsVisible = false;
        try
        {
            var repository = new LocalModRepository(Storage.GetSystemPath(ClientApplication.Paths.ContentPackageCache));
            cached = repository.ListAll();
        }
        catch (Exception exception)
        {
            Log.Warning($"World mod cache listing failed: {exception}");
            cached = [];
            _unavailableLabel.IsVisible = true;
        }

        IEnumerable<ModProfile> profiles = _originalProfile is null ? [_draft] : [_draft, _originalProfile];
        _items = ModManagementCatalog.Build(cached, new ModProfile(), profiles, null)
            .Select(InspectPackage)
            .ToArray();
        RefreshList(selected);
    }

    private static ModItem InspectPackage(ManagedModItem item)
    {
        if (item.LocalEntry is null)
        {
            return new ModItem(item, null, null);
        }

        try
        {
            using var stream = File.OpenRead(item.LocalEntry.Path);
            var package = ModPackage.Read(item.LocalEntry.Path, stream);
            if (!string.Equals(package.Manifest.Id, item.ModId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(package.Manifest.Version, item.Version, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(package.PackageHash, item.PackageHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(Text("PackageIdentityMismatch"));
            }

            return new ModItem(item, package.Manifest.CanRemoveFromWorld, null);
        }
        catch (Exception exception)
        {
            Log.Warning($"World mod declaration inspection failed for '{item.ModId}@{item.Version}': {exception}");
            return new ModItem(item, null, exception.Message);
        }
    }

    private void RefreshList(ModItem? selected)
    {
        var scrollPosition = _modsList.ScrollPosition;
        _modsList.ClearItems();
        foreach (var item in _items.Where(item => !item.Package.IsMissing || IsSelected(item)))
        {
            _modsList.AddItem(item);
            if (selected is not null &&
                string.Equals(item.Package.ModId, selected.Package.ModId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Package.Version, selected.Package.Version, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Package.PackageHash, selected.Package.PackageHash, StringComparison.Ordinal))
            {
                _modsList.SelectedItem = item;
            }
        }

        _modsList.ScrollPosition = scrollPosition;
        _emptyLabel.IsVisible = _modsList.Items.Count == 0;
        _languageRevision = Localization.ClientLanguage.Revision;
        _actions.Refresh();
    }

    private string GetStatus(ModItem item)
    {
        var status = new List<string>();
        if (IsSelected(item))
        {
            status.Add(Text("StatusSelected"));
        }

        status.Add(Text(item.Package.IsMissing ? "StatusMissing" : "StatusCached"));
        status.Add(
            Text(
                item.CanRemoveFromWorld is { } removable
                    ? removable ? "StatusRemovable" : "StatusRequired"
                    : "StatusUnavailable"
            )
        );
        return string.Join(" / ", status);
    }

    private Widget CreateModItemWidget(object item)
    {
        var mod = (ModItem)item;
        var widget = (ContainerWidget)LoadWidget(null, AssetManager.Get<XElement>("Widgets/WorldModItem"), null);
        widget.Children.Find<LabelWidget>("Name")!.Text = mod.Package.LocalEntry?.Name ?? mod.Package.ModId;
        widget.Children.Find<LabelWidget>("Identity")!.Text = $"{mod.Package.ModId}@{mod.Package.Version}";
        var status = widget.Children.Find<LabelWidget>("Status")!;
        status.Text = GetStatus(mod);
        status.Color = mod.CanRemoveFromWorld.HasValue ? new Color(120, 190, 120) : new Color(255, 180, 80);
        widget.Children.Find<LabelWidget>("Hash")!.Text = mod.Package.PackageHash;
        return widget;
    }
}
