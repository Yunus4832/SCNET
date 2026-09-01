using Engine.Graphics;
using Engine.Media;

namespace Game.Managers;

public static class BlocksTexturesManager
{
    private const string _assetExtension = ".png";
    private static readonly List<string> _blockTextureNames = [];

    public static Texture2D DefaultBlocksTexture { get; set; } = null!;

    public static ReadOnlyList<string> ReadOnlyBlockTexturesNames => new(_blockTextureNames);

    public static event Action<string>? BlocksTextureDeleted;

    public static void Initialize()
    {
        Storage.CreateDirectory(GamePaths.BlockTextures);
        DefaultBlocksTexture = ContentManager.Get<Texture2D>("Textures/Blocks");
    }

    public static bool IsBuiltIn(string name)
    {
        return string.IsNullOrEmpty(name);
    }

    public static string GetFileName(string name)
    {
        return IsBuiltIn(name) ? string.Empty : Storage.CombinePaths(GamePaths.BlockTextures, name + _assetExtension);
    }

    public static string GetDisplayName(string name)
    {
        return IsBuiltIn(name) ? "Survivalcraft" : ContentAssetStore.GetDisplayName(GamePaths.BlockTextures, name, _assetExtension);
    }

    public static DateTime GetCreationDate(string name)
    {
        try
        {
            if (!IsBuiltIn(name))
            {
                return Storage.GetFileLastWriteTime(GetFileName(name));
            }
        }
        catch
        {
            // ignored
        }

        return new DateTime(2000, 1, 1);
    }

    public static Texture2D LoadTexture(string name)
    {
        Texture2D? texture2D = null;
        if (!IsBuiltIn(name))
        {
            try
            {
                var image = Image.Load(GetFileName(name));
                ValidateBlocksTexture(image);
                texture2D = Texture2D.Load(image);
                texture2D.Tag = image;
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not load blocks texture \"{name}\". Reason: {ex.Message}.");
            }
        }

        texture2D ??= DefaultBlocksTexture;
        return texture2D;
    }

    private static void ValidateBlocksTexture(Image image)
    {
        if (image.Width > 8192 || image.Height > 8192)
        {
            throw new InvalidOperationException(
                $"Blocks texture is larger than 8192x8192 pixels (size={image.Width}x{image.Height})");
        }

        if (!MathUtils.IsPowerOf2(image.Width) || !MathUtils.IsPowerOf2(image.Height))
        {
            throw new InvalidOperationException(
                $"Blocks texture does not have power-of-two size (size={image.Width}x{image.Height})");
        }
    }

    public static string ImportBlocksTexture(string name, Stream stream)
    {
        var ex = ContentPackageManager.VerifyContentName(name);
        if (ex != null)
        {
            throw ex;
        }

        return ContentAssetStore.Install(GamePaths.BlockTextures, _assetExtension,
            Storage.GetFileNameWithoutExtension(name), stream, ValidateBlocksTexture);
    }

    public static string ReplaceBlocksTexture(string assetKey, string displayName, Stream stream) =>
        ContentAssetStore.Replace(GamePaths.BlockTextures, _assetExtension, assetKey, displayName, stream,
            ValidateBlocksTexture);

    public static void DeleteBlocksTexture(string name)
    {
        try
        {
            var fileName = GetFileName(name);
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            ContentAssetStore.Delete(GamePaths.BlockTextures, name, _assetExtension);
            BlocksTextureDeleted?.Invoke(name);
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser($"Unable to delete blocks texture \"{name}\"", e);
        }
    }

    public static void UpdateBlocksTexturesList()
    {
        _blockTextureNames.Clear();
        _blockTextureNames.Add(string.Empty);
        foreach (var item in Storage.ListFileNames(GamePaths.BlockTextures))
        {
            if (ContentAssetStore.IsComplete(GamePaths.BlockTextures, item, _assetExtension))
                _blockTextureNames.Add(Storage.GetFileNameWithoutExtension(item));
        }
    }

    private static void ValidateBlocksTexture(Stream stream)
    {
        var image = Image.Load(stream);
        if (image.Width > 8192 || image.Height > 8192)
        {
            throw new InvalidOperationException(
                $"Blocks texture is larger than 8192x8192 pixels (size={image.Width}x{image.Height})");
        }

        if (!MathUtils.IsPowerOf2(image.Width) || !MathUtils.IsPowerOf2(image.Height))
        {
            throw new InvalidOperationException(
                $"Blocks texture does not have power-of-two size (size={image.Width}x{image.Height})");
        }
    }
}
