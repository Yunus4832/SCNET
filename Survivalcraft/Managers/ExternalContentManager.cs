using Game.ContentProviders;

namespace Game.Managers;

public static class ExternalContentManager
{
    private static List<IExternalContentProvider> _providers = [];

    private const string _typeName = "ExternalContentManager";

    public static IExternalContentProvider DefaultProvider => Providers.Count <= 0
        ? throw new InvalidOperationException("ContentProvider not found")
        : Providers[0];

    public static ReadOnlyList<IExternalContentProvider> Providers => new(_providers);

    public static void Initialize()
    {
        _providers = new List<IExternalContentProvider>
        {
            new SchubExternalContentProvider(),
#if DESKTOP
            new DiskExternalContentProvider(),
#endif
#if ANDROID
            new AndroidSdCardExternalContentProvider(),
#endif

            new DropboxExternalContentProvider(),
            new TransferShExternalContentProvider()
        };
    }

    public static ExternalContentType ExtensionToType(string extension)
    {
        extension = extension.ToLower();
        foreach (ExternalContentType value in Enum.GetValues(typeof(ExternalContentType)))
        {
            if (GetEntryTypeExtensions(value).FirstOrDefault(e => e == extension) != null)
            {
                return value;
            }
        }

        return ExternalContentType.Unknown;
    }

    public static IEnumerable<string> GetEntryTypeExtensions(ExternalContentType type)
    {
        switch (type)
        {
            case ExternalContentType.World:
                yield return ".scworld";
                break;
            case ExternalContentType.BlocksTexture:
                yield return ".scbtex";
                yield return ".png";
                break;
            case ExternalContentType.CharacterSkin:
                yield return ".scskin";
                break;
            case ExternalContentType.FurniturePack:
                yield return ".scfpack";
                break;
        }
    }

    public static Subtexture GetEntryTypeIcon(ExternalContentType type)
    {
        return type switch
        {
            ExternalContentType.Directory => ContentManager.Get<Subtexture>("Textures/Atlas/FolderIcon"),
            ExternalContentType.World => ContentManager.Get<Subtexture>("Textures/Atlas/WorldIcon"),
            ExternalContentType.BlocksTexture => ContentManager.Get<Subtexture>("Textures/Atlas/TexturePackIcon"),
            ExternalContentType.CharacterSkin => ContentManager.Get<Subtexture>("Textures/Atlas/CharacterSkinIcon"),
            ExternalContentType.FurniturePack => ContentManager.Get<Subtexture>("Textures/Atlas/FurnitureIcon"),
            _ => ContentManager.Get<Subtexture>("Textures/Atlas/QuestionMarkIcon")
        };
    }

    public static string GetEntryTypeDescription(ExternalContentType type)
    {
        return type switch
        {
            ExternalContentType.Directory => LanguageManager.Get(_typeName, "Directory"),
            ExternalContentType.World => LanguageManager.Get(_typeName, "World"),
            ExternalContentType.BlocksTexture => LanguageManager.Get(_typeName, "Blocks Texture"),
            ExternalContentType.CharacterSkin => LanguageManager.Get(_typeName, "Character Skin"),
            ExternalContentType.FurniturePack => LanguageManager.Get(_typeName, "Furniture Pack"),
            _ => string.Empty
        };
    }

    public static bool IsEntryTypeDownloadSupported(ExternalContentType type)
    {
        return type switch
        {
            ExternalContentType.World => true,
            ExternalContentType.BlocksTexture => true,
            ExternalContentType.CharacterSkin => true,
            ExternalContentType.FurniturePack => true,
            _ => false
        };
    }

    public static bool DoesEntryTypeRequireName(ExternalContentType type)
    {
        return type switch
        {
            ExternalContentType.BlocksTexture => true,
            ExternalContentType.CharacterSkin => true,
            ExternalContentType.FurniturePack => true,
            _ => false
        };
    }

    public static Exception? VerifyExternalContentName(string name)
    {
        var trimedName = name.Trim();
        if (string.IsNullOrEmpty(trimedName))
        {
            return new InvalidOperationException(LanguageManager.Get(_typeName, 1));
        }

        return trimedName.Length > 50 ? new InvalidOperationException(LanguageManager.Get(_typeName, 2)) : null;
    }

    public static void DeleteExternalContent(ExternalContentType type, string name)
    {
        switch (type)
        {
            case ExternalContentType.World:
                WorldsManager.DeleteWorld(name);
                break;
            case ExternalContentType.BlocksTexture:
                BlocksTexturesManager.DeleteBlocksTexture(name);
                break;
            case ExternalContentType.CharacterSkin:
                CharacterSkinsManager.DeleteCharacterSkin(name);
                break;
            case ExternalContentType.FurniturePack:
                FurniturePacksManager.DeleteFurniturePack(name);
                break;
            case ExternalContentType.Unknown:
            case ExternalContentType.Directory:
            default:
                throw new InvalidOperationException(LanguageManager.Get(_typeName, 4));
        }
    }

    public static void ImportExternalContent(Stream stream, ExternalContentType type, string name,
        Action<string> success, Action<Exception> failure)
    {
        Task.Run(delegate
        {
            try
            {
                success(ImportExternalContentSync(stream, type, name));
            }
            catch (Exception obj)
            {
                failure(obj);
            }
        });
    }

    public static string ImportExternalContentSync(Stream stream, ExternalContentType type, string name)
    {
        return type switch
        {
            ExternalContentType.World => WorldsManager.ImportWorld(stream),
            ExternalContentType.BlocksTexture => BlocksTexturesManager.ImportBlocksTexture(name, stream),
            ExternalContentType.CharacterSkin => CharacterSkinsManager.ImportCharacterSkin(name, stream),
            ExternalContentType.FurniturePack => FurniturePacksManager.ImportFurniturePack(name, stream),
            _ => throw new InvalidOperationException(LanguageManager.Get(_typeName, 4))
        };
    }

    public static void ShowLoginUiIfNeeded(IExternalContentProvider provider, bool showWarningDialog, Action handler)
    {
        if (provider is { RequiresLogin: true, IsLoggedIn: false })
        {
            void LoginAction()
            {
                var busyDialog = new CancellableBusyDialog(LanguageManager.Get(_typeName, 5), true);
                DialogsManager.ShowDialog(null, busyDialog);
                provider.Login(
                    busyDialog.Progress,
                    delegate
                    {
                        DialogsManager.HideDialog(busyDialog);
                        handler.Invoke();
                    },
                    delegate(Exception error)
                    {
                        DialogsManager.HideDialog(busyDialog);
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageManager.Get("Usual", "error"),
                                error.Message,
                                LanguageManager.Get("Usual", "ok")
                            )
                        );
                    });
            }

            if (showWarningDialog)
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageManager.Get(_typeName, 6),
                        string.Format(LanguageManager.Get(_typeName, 7), provider.DisplayName),
                        LanguageManager.Get(_typeName, 8),
                        LanguageManager.Get("Usual", "cancel"),
                        delegate(MessageDialogButton b)
                        {
                            if (b == MessageDialogButton.Button1)
                            {
                                LoginAction();
                            }
                        }
                    )
                );
            }
            else
            {
                LoginAction();
            }
        }
        else
        {
            handler.Invoke();
        }
    }

    public static void ShowUploadUi(ExternalContentType type, string name)
    {
        DialogsManager.ShowDialog(
            null,
            new SelectExternalContentProviderDialog(
                LanguageManager.Get(_typeName, 9),
                false,
                delegate(IExternalContentProvider provider)
                {
                    try
                    {
                        ShowLoginUiIfNeeded(
                            provider,
                            true,
                            delegate
                            {
                                var busyDialog = new CancellableBusyDialog(LanguageManager.Get(_typeName, 10), false);
                                DialogsManager.ShowDialog(null, busyDialog);
                                Task.Run(delegate
                                {
                                    var needsDelete = false;
                                    string? sourcePath = null;
                                    Stream? stream = null;

                                    try
                                    {
                                        string path;
                                        if (type == ExternalContentType.BlocksTexture)
                                        {
                                            sourcePath = BlocksTexturesManager.GetFileName(name);
                                            if (string.IsNullOrEmpty(sourcePath))
                                            {
                                                throw new InvalidOperationException(LanguageManager.Get(_typeName, 11));
                                            }

                                            path = Storage.GetFileName(sourcePath);
                                        }
                                        else if (type == ExternalContentType.CharacterSkin)
                                        {
                                            if (CharacterSkinsManager.GetFileName(name, out sourcePath))
                                            {
                                                throw new InvalidOperationException(LanguageManager.Get(_typeName, 11));
                                            }

                                            path = Storage.GetFileName(sourcePath);
                                        }
                                        else if (type == ExternalContentType.FurniturePack)
                                        {
                                            sourcePath = FurniturePacksManager.GetFileName(name);
                                            if (string.IsNullOrEmpty(sourcePath))
                                            {
                                                throw new InvalidOperationException(LanguageManager.Get(_typeName, 11));
                                            }

                                            path = Storage.GetFileName(sourcePath);
                                        }
                                        else
                                        {
                                            if (type != ExternalContentType.World)
                                            {
                                                throw new InvalidOperationException(LanguageManager.Get(_typeName, 12));
                                            }

                                            busyDialog.LargeMessage = LanguageManager.Get(_typeName, 13);
                                            if (!Storage.DirectoryExists(GamePaths.External + "/files"))
                                            {
                                                Storage.CreateDirectory(GamePaths.External + "/files");
                                            }

                                            sourcePath = GamePaths.External + "/files/WorldUpload.tmp";
                                            needsDelete = true;
                                            var worldInfo = WorldsManager.GetWorldInfo(name);
                                            if (worldInfo is null)
                                            {
                                                return;
                                            }

                                            var name2 = worldInfo.WorldSettings.Name;
                                            path = $"{name2}.scworld";
                                            using var targetStream = Storage.OpenFile(sourcePath, OpenFileMode.Create);
                                            WorldsManager.ExportWorld(name, targetStream);
                                        }

                                        busyDialog.LargeMessage = LanguageManager.Get(_typeName, 14);
                                        stream = Storage.OpenFile(sourcePath, OpenFileMode.Read);
                                        provider.Upload(path, stream, busyDialog.Progress, delegate(string link)
                                            {
                                                var length = stream.Length;
                                                Cleanup();
                                                DialogsManager.HideDialog(busyDialog);
                                                if (string.IsNullOrEmpty(link))
                                                {
                                                    DialogsManager.ShowDialog(
                                                        null,
                                                        new MessageDialog(
                                                            LanguageManager.Get("Usual", "success"),
                                                            string.Format(LanguageManager.Get(_typeName, 15),
                                                                DataSizeFormatter.Format(length)),
                                                            LanguageManager.Get("Usual", "ok")
                                                        )
                                                    );
                                                }
                                                else
                                                {
                                                    DialogsManager.ShowDialog(null,
                                                        new ExternalContentLinkDialog(link));
                                                }
                                            },
                                            delegate(Exception error)
                                            {
                                                Cleanup();
                                                DialogsManager.HideDialog(busyDialog);
                                                DialogsManager.ShowDialog(
                                                    null,
                                                    new MessageDialog(
                                                        LanguageManager.Get("Usual", "error"),
                                                        error.Message,
                                                        LanguageManager.Get("Usual", "ok")
                                                    )
                                                );
                                            });
                                    }
                                    catch (Exception ex2)
                                    {
                                        Cleanup();
                                        DialogsManager.HideDialog(busyDialog);
                                        DialogsManager.ShowDialog(
                                            null,
                                            new MessageDialog(
                                                LanguageManager.Get("Usual", "error"),
                                                ex2.Message,
                                                LanguageManager.Get("Usual", "ok")
                                            )
                                        );
                                    }

                                    return;

                                    void Cleanup()
                                    {
                                        Utilities.Dispose(ref stream);
                                        if (!needsDelete || sourcePath == null)
                                        {
                                            return;
                                        }

                                        try
                                        {
                                            Storage.DeleteFile(sourcePath);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                });
                            });
                    }
                    catch (Exception ex)
                    {
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageManager.Get("Usual", "error"),
                                ex.Message,
                                LanguageManager.Get("Usual", "ok")
                            )
                        );
                    }
                }));
    }
}
