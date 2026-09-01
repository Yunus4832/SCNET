using System.Xml.Linq;

using Content.Packaging;

using Game.Content;

namespace Game.Screens;

public sealed class ContentServerScreen : Screen
{
    private const string _typeName = nameof(ContentServerScreen);

    private readonly ListPanelWidget _contentList;
    private readonly ButtonWidget _downloadButton;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _uninstallButton;
    private bool _busy;

    public ContentServerScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentServerScreen");
        LoadContents(this, node);
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _downloadButton = Children.Find<ButtonWidget>("Download")!;
        _uninstallButton = Children.Find<ButtonWidget>("Uninstall")!;
        _refreshButton = Children.Find<ButtonWidget>("Refresh")!;
        _contentList.ItemWidgetFactory = item =>
        {
            var content = (ContentCatalogItem)item;
            var widget = (ContainerWidget)LoadWidget(
                this, ContentManager.Get<XElement>("Widgets/ContentServerItem"), null);
            widget.Children.Find<LabelWidget>("ContentServerItem.Name")!.Text =
                $"{content.Name}  {content.Version}";
            widget.Children.Find<LabelWidget>("ContentServerItem.Details")!.Text =
                $"{content.Type} | {content.Identifier} | {DataSizeFormatter.Format(content.PackageSize)}";
            return widget;
        };
    }

    public override void Enter(object[] parameters)
    {
        Refresh();
    }

    public override void Update()
    {
        var selected = _contentList.SelectedItem as ContentCatalogItem;
        _downloadButton.IsEnabled = !_busy && selected is not null;
        _uninstallButton.IsEnabled = false;
        _refreshButton.IsEnabled = !_busy;

        if (_refreshButton.IsClicked)
        {
            Refresh();
        }
        if (_downloadButton.IsClicked && selected is not null)
        {
            Install(selected);
        }
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void Refresh()
    {
        var serverUrl = SettingsManager.Current.ContentServerUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            _contentList.ClearItems();
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ServerNotConfigured"));
            return;
        }

        _busy = true;
        Task.Run(async () =>
        {
            try
            {
                using var client = new ContentServerClient(serverUrl);
                var items = await client.ListAsync();
                Dispatcher.Dispatch(() =>
                {
                    _contentList.ClearItems();
                    foreach (var item in items)
                    {
                        _contentList.AddItem(item);
                    }
                    _busy = false;
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ShowError(exception);
                });
            }
        });
    }

    private void Install(ContentCatalogItem item)
    {
        _busy = true;
        Task.Run(async () =>
        {
            try
            {
                using var client = new ContentServerClient(SettingsManager.Current.ContentServerUrl);
                var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
                var cached = await client.DownloadToCacheAsync(item, cache);
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    SelectInstallMode(cached);
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ShowError(exception);
                });
            }
        });
    }

    private void SelectInstallMode(ContentPackageCacheEntry cached)
    {
        if (cached.Type == ContentPackageType.Mod)
        {
            RunInstallation(cached.PackageHash, null);
            return;
        }

        var replacements = GetReplacementTargets(cached.Type);
        if (replacements.Count == 0)
        {
            ConfirmCreate(cached);
            return;
        }
        var create = LanguageManager.Get(_typeName, "CreateNew");
        var replace = LanguageManager.Get(_typeName, "ReplaceExisting");
        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "InstallModeTitle"), new[] { create, replace }, 64f,
            item => (string)item,
            item =>
            {
                if ((string)item == create) ConfirmCreate(cached);
                else SelectReplacement(cached, replacements);
            }));
    }

    private void ConfirmCreate(ContentPackageCacheEntry cached)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "InstallModeTitle"),
            string.Format(LanguageManager.Get(_typeName, "ConfirmCreate"), cached.Name),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"),
            button =>
            {
                if (button == MessageDialogButton.Button1) RunInstallation(cached.PackageHash, null);
            }));
    }

    private void SelectReplacement(ContentPackageCacheEntry cached, IReadOnlyList<ReplacementTarget> targets)
    {
        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "SelectReplacement"), targets, 64f,
            item => ((ReplacementTarget)item).DisplayName,
            item => ConfirmReplacement(cached, (ReplacementTarget)item)));
    }

    private void ConfirmReplacement(ContentPackageCacheEntry cached, ReplacementTarget target)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "ReplaceExisting"),
            string.Format(LanguageManager.Get(_typeName, "ConfirmReplace"), cached.Name, target.DisplayName,
                target.ReferenceCount),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"),
            button =>
            {
                if (button == MessageDialogButton.Button1)
                    RunInstallation(cached.PackageHash, new ContentInstallOptions(target.AssetKey));
            }));
    }

    private void RunInstallation(string packageHash, ContentInstallOptions? options)
    {
        _busy = true;
        Task.Run(() =>
        {
            try
            {
                var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
                ContentPackageWorkflow.InstallCached(cache, packageHash, options);
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "Installed"));
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ShowError(exception);
                });
            }
        });
    }

    private static IReadOnlyList<ReplacementTarget> GetReplacementTargets(ContentPackageType type)
    {
        if (type == ContentPackageType.World)
        {
            WorldsManager.UpdateWorldsList();
            var running = GameManager.Project?.FindSubsystem<SubsystemGameInfo>()?.DirectoryName;
            return WorldsManager.WorldInfos.Where(world =>
                    !string.Equals(world.DirectoryName, running, StringComparison.OrdinalIgnoreCase))
                .Select(world => new ReplacementTarget(Storage.GetFileName(world.DirectoryName),
                    world.WorldSettings.Name, 0)).ToArray();
        }
        if (type == ContentPackageType.BlocksTexture)
        {
            WorldsManager.UpdateWorldsList();
            BlocksTexturesManager.UpdateBlocksTexturesList();
            return BlocksTexturesManager.ReadOnlyBlockTexturesNames.Where(name => !BlocksTexturesManager.IsBuiltIn(name))
                .Select(name => new ReplacementTarget(name, BlocksTexturesManager.GetDisplayName(name),
                    WorldsManager.WorldInfos.Count(world => world.WorldSettings.BlocksTextureName == name))).ToArray();
        }
        if (type == ContentPackageType.CharacterSkin)
        {
            WorldsManager.UpdateWorldsList();
            CharacterSkinsManager.UpdateCharacterSkinsList();
            return CharacterSkinsManager.ReadOnlyCharacterSkinsNames.Where(name => !CharacterSkinsManager.IsBuiltIn(name))
                .Select(name => new ReplacementTarget(name, CharacterSkinsManager.GetDisplayName(name),
                    WorldsManager.WorldInfos.Count(world => world.PlayerInfos.Any(player => player.CharacterSkinName == name))))
                .ToArray();
        }
        FurniturePacksManager.UpdateFurniturePacksList();
        return FurniturePacksManager.ReadOnlyFurniturePackNames
            .Select(name => new ReplacementTarget(name, FurniturePacksManager.GetDisplayName(name), 0)).ToArray();
    }

    private sealed record ReplacementTarget(string AssetKey, string DisplayName, int ReferenceCount);

    private static void ShowError(Exception exception)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get("Usual", "error"), exception.Message, LanguageManager.Get("Usual", "ok")));
    }
}
