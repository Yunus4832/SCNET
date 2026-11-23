namespace Game.AssetReaders;

public abstract class AssetReader
{
    public abstract string Type { get; }

    public abstract string[] DefaultSuffix { get; }

    public abstract object Get(AssetInfo[] contents);
}
