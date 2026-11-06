using Content.Packaging;

using Game.Application;

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
            failed(
                new InvalidOperationException(Localization.ClientLanguage.Catalog.Get(_typeName, "PickerUnavailable"))
            );
            return;
        }

        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "SelectType"),
                _types,
                64f,
                item => GetTypeName((ContentPackageType)item),
                item => SelectSource((ContentPackageType)item, setBusy, saved, failed)
            )
        );
    }

    private static void SelectSource(
        ContentPackageType type,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        if (type == ContentPackageType.World)
        {
            ClientApplication.Worlds.UpdateWorldsList();
            var running = GameManager.ClientProject?.FindSubsystem<SubsystemGameInfo>()?.DirectoryName;
            var sources = ClientApplication.Worlds.WorldInfos.Where(world =>
                    !string.Equals(world.DirectoryName, running, StringComparison.OrdinalIgnoreCase)
                )
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

    private static void SelectAsset(
        ContentPackageType type,
        IReadOnlyList<AssetSource> sources,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        if (sources.Count == 0)
        {
            failed(new InvalidOperationException(Localization.ClientLanguage.Catalog.Get(_typeName, "NoSources")));
            return;
        }

        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "SelectSource"),
                sources,
                64f,
                item => ((AssetSource)item).DisplayName,
                item => AskIdentity(type, (AssetSource)item, null, setBusy, saved, failed)
            )
        );
    }

    private static async void SelectImage(
        ContentPackageType type,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        try
        {
            var files = await FilePicker.PickFilesAsync(
                new FilePickerRequest(
                    [".webp", ".png"],
                    false,
                    Localization.ClientLanguage.Catalog.Get(_typeName, "SelectImage")
                )
            );
            if (files.Count == 0)
            {
                return;
            }

            var file = files[0];
            Dispatcher.Dispatch(() => AskIdentity(
                    type,
                    new AssetSource(string.Empty, Path.GetFileNameWithoutExtension(file.Name)),
                    file,
                    setBusy,
                    saved,
                    failed
                )
            );
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() => failed(exception));
        }
    }

    private static void AskIdentity(
        ContentPackageType type,
        AssetSource source,
        PickedFile? image,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        DialogsManager.ShowDialog(
            null,
            new TextBoxDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "Name"),
                source.DisplayName,
                80,
                name =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        failed(
                            new ContentPackageException(
                                Localization.ClientLanguage.Catalog.Get(_typeName, "NameRequired")
                            )
                        );
                        return;
                    }

                    DialogsManager.ShowDialog(
                        null,
                        new TextBoxDialog(
                            Localization.ClientLanguage.Catalog.Get(_typeName, "Version"),
                            "1.0.0",
                            40,
                            version => AskDescription(
                                type,
                                source,
                                image,
                                name.Trim(),
                                version.Trim(),
                                setBusy,
                                saved,
                                failed
                            ),
                            false
                        )
                    );
                },
                false
            )
        );
    }

    private static void AskDescription(
        ContentPackageType type,
        AssetSource source,
        PickedFile? image,
        string name,
        string version,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        DialogsManager.ShowDialog(
            null,
            new TextBoxDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "Summary"),
                string.Empty,
                160,
                summary => DialogsManager.ShowDialog(
                    null,
                    new TextBoxDialog(
                        Localization.ClientLanguage.Catalog.Get(_typeName, "Description"),
                        string.Empty,
                        4000,
                        description => _ = Create(
                            type,
                            source,
                            image,
                            name,
                            summary.Trim(),
                            description.Trim(),
                            version,
                            setBusy,
                            saved,
                            failed
                        ),
                        false
                    )
                ),
                false
            )
        );
    }

    private static async Task Create(
        ContentPackageType type,
        AssetSource source,
        PickedFile? image,
        string name,
        string summary,
        string description,
        string version,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        setBusy(true);
        try
        {
            ContentPackageCreationArtifact artifact;
            if (image is not null)
            {
                await using var input = await image.OpenReadAsync(CancellationToken.None);
                artifact = await Task.Run(() => ContentPackageCreationManager.CreateImage(
                        type,
                        new ContentCreationIdentity(name, summary, description, version),
                        input
                    )
                );
            }
            else
            {
                artifact = await Task.Run(() => type == ContentPackageType.World
                    ? ContentPackageCreationManager.CreateWorld(
                        new ContentCreationIdentity(name, summary, description, version),
                        source.AssetKey
                    )
                    : ContentPackageCreationManager.CreateFurniture(
                        new ContentCreationIdentity(name, summary, description, version),
                        source.AssetKey
                    )
                );
            }

            await using var validationStream = artifact.OpenRead();
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

    private static void ShowPreview(
        ContentPackageCreationArtifact artifact,
        ContentPackageInspection inspection,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        var manifest = inspection.Manifest;
        var preview = string.Format(
                          Localization.ClientLanguage.Catalog.Get(_typeName, "Preview"),
                          GetTypeName(manifest.Type),
                          manifest.Name,
                          manifest.Version,
                          manifest.Identifier,
                          artifact.PackageHash
                      ) +
                      $"\n{manifest.Summary}\n{manifest.Description}";
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "PreviewTitle"),
                preview,
                Localization.ClientLanguage.Catalog.Get(_typeName, "Save"),
                Localization.ClientLanguage.Catalog.Get("Usual", "cancel"),
                button =>
                {
                    if (button == MessageDialogButton.Button1)
                    {
                        _ = SaveArtifact(artifact, manifest, setBusy, saved, failed);
                    }
                    else
                    {
                        artifact.Dispose();
                    }
                }
            )
        );
    }

    private static async Task SaveArtifact(
        ContentPackageCreationArtifact artifact,
        ContentPackageManifest manifest,
        Action<bool> setBusy,
        Action saved,
        Action<Exception> failed
    )
    {
        setBusy(true);
        try
        {
            var target = await FilePicker.PickSaveTargetAsync(
                new FileSaveRequest(
                    MakeFileName($"{manifest.Name}-{manifest.Version}") + ContentPackageReader.FileExtension,
                    "application/vnd.scnet.content-package",
                    Localization.ClientLanguage.Catalog.Get(_typeName, "SaveTitle")
                )
            );
            if (target is null)
            {
                return;
            }

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
        ContentPackageType.World => Localization.ClientLanguage.Catalog.Get(_typeName, "World"),
        ContentPackageType.BlocksTexture => Localization.ClientLanguage.Catalog.Get(_typeName, "BlocksTexture"),
        ContentPackageType.CharacterSkin => Localization.ClientLanguage.Catalog.Get(_typeName, "CharacterSkin"),
        ContentPackageType.FurniturePack => Localization.ClientLanguage.Catalog.Get(_typeName, "FurniturePack"),
        _ => type.ToString()
    };

    private static string MakeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private sealed record AssetSource(string AssetKey, string DisplayName);
}
