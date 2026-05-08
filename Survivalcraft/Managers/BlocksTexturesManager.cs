using Engine.Graphics;
using Engine.Media;

namespace Game.Managers;

public static class BlocksTexturesManager
{
    private static readonly List<string> _blockTextureNames = [];

    public static Texture2D DefaultBlocksTexture { get; set; } = null!;

    public static ReadOnlyList<string> ReadOnlyBlockTexturesNames => new(_blockTextureNames);

    public static event Action<string>? BlocksTextureDeleted;

    public static void Initialize()
    {
        Storage.CreateDirectory(ModsManager.BlockTexturesDirectoryName);
        DefaultBlocksTexture = ContentManager.Get<Texture2D>("Textures/Blocks");
    }

    public static bool IsBuiltIn(string name)
    {
        return string.IsNullOrEmpty(name);
    }

    public static string GetFileName(string name)
    {
        return IsBuiltIn(name) ? string.Empty : Storage.CombinePaths(ModsManager.BlockTexturesDirectoryName, name);
    }

    public static string GetDisplayName(string name)
    {
        return IsBuiltIn(name) ? "Survivalcraft" : Storage.GetFileNameWithoutExtension(name);
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
        var ex = ExternalContentManager.VerifyExternalContentName(name);
        if (ex != null)
        {
            throw ex;
        }

        if (Storage.GetExtension(name) != ".scbtex")
        {
            name += ".scbtex";
        }

        ValidateBlocksTexture(stream);
        stream.Position = 0L;
        using var destination = Storage.OpenFile(GetFileName(name), OpenFileMode.Create);
        stream.CopyTo(destination);
        return name;
    }

    public static void DeleteBlocksTexture(string name)
    {
        try
        {
            var fileName = GetFileName(name);
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            Storage.DeleteFile(fileName);
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
        foreach (var item in Storage.ListFileNames(ModsManager.BlockTexturesDirectoryName))
        {
            _blockTextureNames.Add(item);
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
