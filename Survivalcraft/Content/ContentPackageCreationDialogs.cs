using Content.Packaging;

using Engine.FileStorage;

using Game.Managers;

namespace Game.Content;

public static class ContentPackageCreationDialogs
{
    private const string _typeName = nameof(ContentPackageCreationDialogs);
    private static readonly ContentPackageType[] _types =
    [
        ContentPackageType.World,
        ContentPackageType.BlocksTexture,
        ContentPackageType.CharacterSkin,
        ContentPackageType.FurniturePack
    ];

    public static void Show(Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(failed);
        if (!FilePicker.IsAvailable)
        {
            failed(new InvalidOperationException(LanguageManager.Get(_typeName, "PickerUnavailable")));
            return;
        }

        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "SelectType"), _types, 64f,
            item => GetTypeName((ContentPackageType)item),
            item => SelectSource((ContentPackageType)item, setBusy, saved, failed)));
    }

    private static void SelectSource(ContentPackageType type, Action<bool> setBusy, Action saved,
        Action<Exception> failed)
    {
        if (type == ContentPackageType.World)
        {
            WorldsManager.UpdateWorldsList();
            var running = GameManager.Project?.FindSubsystem<SubsystemGameInfo>()?.DirectoryName;
            var sources = WorldsManager.WorldInfos.Where(world =>
                    !string.Equals(world.DirectoryName, running, StringComparison.OrdinalIgnoreCase))
                .Select(world => new AssetSource(Storage.GetFileName(world.DirectoryName), world.WorldSettings.Name))
                .ToArray();
            SelectAsset(type, sources, setBusy, saved, failed);
            return;
        }

        if (type == ContentPackageType.FurniturePack)
        {
            FurniturePacksManager.UpdateFurniturePacksList();
            var sources = FurniturePacksManager.ReadOnlyFurniturePackNames
                .Select(name => new AssetSource(name, FurniturePacksManager.GetDisplayName(name))).ToArray();
            SelectAsset(type, sources, setBusy, saved, failed);
            return;
        }

        SelectImage(type, setBusy, saved, failed);
    }

    private static void SelectAsset(ContentPackageType type, IReadOnlyList<AssetSource> sources,
        Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        if (sources.Count == 0)
        {
            failed(new InvalidOperationException(LanguageManager.Get(_typeName, "NoSources")));
            return;
        }
        DialogsManager.ShowDialog(null, new ListSelectionDialog(
            LanguageManager.Get(_typeName, "SelectSource"), sources, 64f,
            item => ((AssetSource)item).DisplayName,
            item => AskIdentity(type, (AssetSource)item, null, setBusy, saved, failed)));
    }

    private static async void SelectImage(ContentPackageType type, Action<bool> setBusy, Action saved,
        Action<Exception> failed)
    {
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest([".png"], false,
                LanguageManager.Get(_typeName, "SelectImage")));
            if (files.Count == 0) return;
            var file = files[0];
            Dispatcher.Dispatch(() => AskIdentity(type,
                new AssetSource(string.Empty, Path.GetFileNameWithoutExtension(file.Name)), file,
                setBusy, saved, failed));
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() => failed(exception));
        }
    }

    private static void AskIdentity(ContentPackageType type, AssetSource source, PickedFile? image,
        Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        DialogsManager.ShowDialog(null, new TextBoxDialog(
            LanguageManager.Get(_typeName, "Name"), source.DisplayName, 80,
            name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    failed(new ContentPackageException(LanguageManager.Get(_typeName, "NameRequired")));
                    return;
                }
                DialogsManager.ShowDialog(null, new TextBoxDialog(
                    LanguageManager.Get(_typeName, "Version"), "1.0.0", 40,
                    version => Create(type, source, image, name.Trim(), version.Trim(), setBusy, saved, failed), false));
            }, false));
    }

    private static async void Create(ContentPackageType type, AssetSource source, PickedFile? image,
        string name, string version, Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        setBusy(true);
        try
        {
            ContentPackageCreationArtifact artifact;
            if (image is not null)
            {
                await using var input = await image.OpenReadAsync(CancellationToken.None);
                artifact = await Task.Run(() => ContentPackageCreationManager.CreateImage(type,
                    new ContentCreationIdentity(name, version), input));
            }
            else
            {
                artifact = await Task.Run(() => type == ContentPackageType.World
                    ? ContentPackageCreationManager.CreateWorld(new ContentCreationIdentity(name, version), source.AssetKey)
                    : ContentPackageCreationManager.CreateFurniture(new ContentCreationIdentity(name, version), source.AssetKey));
            }

            using var validationStream = artifact.OpenRead();
            var inspection = ContentPackageReader.Inspect(validationStream);
            Dispatcher.Dispatch(() => ShowPreview(artifact, inspection, setBusy, saved, failed));
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() => failed(exception));
        }
        finally
        {
            setBusy(false);
        }
    }

    private static void ShowPreview(ContentPackageCreationArtifact artifact, ContentPackageInspection inspection,
        Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        var manifest = inspection.Manifest;
        var preview = string.Format(LanguageManager.Get(_typeName, "Preview"), GetTypeName(manifest.Type),
            manifest.Name, manifest.Version, manifest.Identifier, artifact.PackageHash);
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "PreviewTitle"), preview,
            LanguageManager.Get(_typeName, "Save"), LanguageManager.Get("Usual", "cancel"),
            button =>
            {
                if (button == MessageDialogButton.Button1)
                    SaveArtifact(artifact, manifest, setBusy, saved, failed);
                else
                    artifact.Dispose();
            }));
    }

    private static async void SaveArtifact(ContentPackageCreationArtifact artifact, ContentPackageManifest manifest,
        Action<bool> setBusy, Action saved, Action<Exception> failed)
    {
        setBusy(true);
        try
        {
            var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest(
                MakeFileName($"{manifest.Name}-{manifest.Version}") + ContentPackageReader.FileExtension,
                "application/vnd.scnet.content-package", LanguageManager.Get(_typeName, "SaveTitle")));
            if (target is null) return;
            await using var source = artifact.OpenRead();
            await using var destination = await target.OpenWriteAsync(CancellationToken.None);
            await source.CopyToAsync(destination, 64 * 1024);
            await destination.FlushAsync();
            Dispatcher.Dispatch(saved);
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() => failed(exception));
        }
        finally
        {
            artifact.Dispose();
            setBusy(false);
        }
    }

    private static string GetTypeName(ContentPackageType type) => type switch
    {
        ContentPackageType.World => LanguageManager.Get(_typeName, "World"),
        ContentPackageType.BlocksTexture => LanguageManager.Get(_typeName, "BlocksTexture"),
        ContentPackageType.CharacterSkin => LanguageManager.Get(_typeName, "CharacterSkin"),
        ContentPackageType.FurniturePack => LanguageManager.Get(_typeName, "FurniturePack"),
        _ => type.ToString()
    };

    private static string MakeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private sealed record AssetSource(string AssetKey, string DisplayName);
}
