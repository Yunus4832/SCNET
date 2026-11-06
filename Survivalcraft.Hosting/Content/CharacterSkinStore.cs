using Engine.Media.Images;

namespace Game.Content;

public sealed class CharacterSkinStore(string directory)
{
    public bool Contains(string name) => BuiltInCharacterSkins.IsBuiltIn(name) || TryGetFileName(name, out _);

    public bool TryGetFileName(string name, out string filename)
    {
        filename = string.Empty;
        if (string.IsNullOrEmpty(name) || BuiltInCharacterSkins.IsBuiltIn(name)) { return false; }

        var extension = ContentAssetStore.FindImageExtension(directory, name);
        if (extension is null) { return false; }

        filename = Storage.CombinePaths(directory, name + extension);
        return true;
    }

    public void Save(string name, byte[] data)
    {
        using var source = new MemoryStream(data, false);
        if (TryGetFileName(name, out var path))
        {
            ContentAssetStore.Replace(
                directory,
                null,
                name,
                ContentAssetStore.GetDisplayName(directory, name, Storage.GetExtension(path)),
                source,
                Validate
            );
        }
        else
        {
            ContentAssetStore.Install(directory, null, name, source, Validate, name);
        }
    }

    public static void Validate(Stream stream)
    {
        var image = Image.Load(stream);
        if (image.Width > 1024 || image.Height > 1024)
        {
            throw new InvalidOperationException(
                $"Character skin is larger than 1024x1024 pixels (size={image.Width}x{image.Height})"
            );
        }

        if (!MathUtils.IsPowerOf2(image.Width) || !MathUtils.IsPowerOf2(image.Height))
        {
            throw new InvalidOperationException(
                $"Character skin does not have power-of-two size (size={image.Width}x{image.Height})"
            );
        }
    }
}
