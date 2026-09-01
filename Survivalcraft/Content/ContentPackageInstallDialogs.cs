using Content.Packaging;

using Game.Managers;

namespace Game.Content;

public static class ContentPackageInstallDialogs
{
    private const string _typeName = nameof(ContentPackageInstallDialogs);

    public static void Show(ContentPackageCacheEntry cached, Action<bool> setBusy, Action installed,
        Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(cached);
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(failed);
        if (cached.Type == ContentPackageType.Mod)
        {
            failed(new ContentPackageException("Mod packages must be installed through mod management."));
            return;
        }

        var replacements = GetReplacementTargets(cached.Type);
        if (replacements.Count == 0)
        {
            ConfirmCreate(cached, setBusy, installed, failed);
            return;
        }

        var create = LanguageManager.Get(_typeName, "CreateNew");
        var replace = LanguageManager.Get(_typeName, "ReplaceExisting");
        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "InstallModeTitle"), new[] { create, replace }, 64f,
            item => (string)item,
            item =>
            {
                if ((string)item == create) ConfirmCreate(cached, setBusy, installed, failed);
                else SelectReplacement(cached, replacements, setBusy, installed, failed);
            }));
    }

    private static void ConfirmCreate(ContentPackageCacheEntry cached, Action<bool> setBusy, Action installed,
        Action<Exception> failed)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "InstallModeTitle"),
            string.Format(LanguageManager.Get(_typeName, "ConfirmCreate"), cached.Name),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"),
            button =>
            {
                if (button == MessageDialogButton.Button1) RunInstallation(cached, null, setBusy, installed, failed);
            }));
    }

    private static void SelectReplacement(ContentPackageCacheEntry cached, IReadOnlyList<ReplacementTarget> targets,
        Action<bool> setBusy, Action installed, Action<Exception> failed)
    {
        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "SelectReplacement"), targets, 64f,
            item => ((ReplacementTarget)item).DisplayName,
            item => ConfirmReplacement(cached, (ReplacementTarget)item, setBusy, installed, failed)));
    }

    private static void ConfirmReplacement(ContentPackageCacheEntry cached, ReplacementTarget target,
        Action<bool> setBusy, Action installed, Action<Exception> failed)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "ReplaceExisting"),
            string.Format(LanguageManager.Get(_typeName, "ConfirmReplace"), cached.Name, target.DisplayName,
                target.ReferenceCount),
            LanguageManager.Get("Usual", "yes"), LanguageManager.Get("Usual", "no"),
            button =>
            {
                if (button == MessageDialogButton.Button1)
                    RunInstallation(cached, new ContentInstallOptions(target.AssetKey), setBusy, installed, failed);
            }));
    }

    private static void RunInstallation(ContentPackageCacheEntry cached, ContentInstallOptions? options, Action<bool> setBusy,
        Action installed, Action<Exception> failed)
    {
        setBusy(true);
        Task.Run(() =>
        {
            try
            {
                var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
                ContentPackageWorkflow.InstallCached(cache, cached.PackageHash, options);
                RefreshInstalledAssets(cached.Type);
                Dispatcher.Dispatch(() =>
                {
                    setBusy(false);
                    installed();
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    setBusy(false);
                    failed(exception);
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

    private static void RefreshInstalledAssets(ContentPackageType type)
    {
        switch (type)
        {
            case ContentPackageType.World:
                WorldsManager.UpdateWorldsList();
                break;
            case ContentPackageType.BlocksTexture:
                BlocksTexturesManager.UpdateBlocksTexturesList();
                break;
            case ContentPackageType.CharacterSkin:
                CharacterSkinsManager.UpdateCharacterSkinsList();
                break;
            case ContentPackageType.FurniturePack:
                FurniturePacksManager.UpdateFurniturePacksList();
                break;
        }
    }

    private sealed record ReplacementTarget(string AssetKey, string DisplayName, int ReferenceCount);
}
