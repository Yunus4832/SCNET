namespace Game.Modding.Blocks;

public sealed record BlockRegistration(int LegacyIndex, Func<Block> Factory);

public sealed record BlockDataRegistration(Func<string> Read);

public static class BlockExtensions
{
    public const string RegistryName = "blocks";

    public const string DataRegistryName = "block_data";

    public static IDisposable RegisterBlock<TBlock>(
        this IModExtensions extensions,
        ResourceId id,
        int legacyIndex
    ) where TBlock : Block, new()
    {
        return extensions.Register(
            RegistryName,
            id,
            new BlockRegistration(legacyIndex, static () => new TBlock()));
    }

    internal static IDisposable RegisterBlock(
        this IModExtensions extensions,
        ResourceId id,
        int legacyIndex,
        Type blockType
    )
    {
        return extensions.Register(
            RegistryName,
            id,
            new BlockRegistration(
                legacyIndex,
                () => (Block)(Activator.CreateInstance(blockType)
                              ?? throw new InvalidOperationException(
                                  $"Could not create block {blockType.FullName}."))));
    }

    public static IDisposable RegisterBlockData(
        this IModExtensions extensions,
        ResourceId id,
        Func<string> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return extensions.Register(DataRegistryName, id, new BlockDataRegistration(read));
    }
}
