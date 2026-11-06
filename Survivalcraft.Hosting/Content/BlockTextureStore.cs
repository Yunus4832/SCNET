namespace Game.Content;

public sealed class BlockTextureStore(string directory)
{
    public string GetFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) { return string.Empty; }

        var extension = ContentAssetStore.FindImageExtension(directory, name) ??
                        throw new InvalidOperationException($"Blocks texture '{name}' does not exist.");
        return Storage.CombinePaths(directory, name + extension);
    }
}
