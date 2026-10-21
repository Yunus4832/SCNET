namespace Game.Modding.Blocks;

public sealed record BlockRegistration(Func<Block> Factory);

public sealed record BlockDataRegistration(Func<string> Read);

public static class BlockExtensions
{
    public const string RegistryName = "blocks";

    public const string DataRegistryName = "block_data";

    public static IDisposable RegisterBlock<TBlock>(
        this IModExtensions extensions,
        ResourceId id
    ) where TBlock : Block, new()
    {
        return extensions.Register(
            RegistryName,
            id,
            new BlockRegistration(static () => new TBlock())
        );
    }

    public static IDisposable RegisterBlockData(
        this IModExtensions extensions,
        ResourceId id,
        Func<string> read
    )
    {
        ArgumentNullException.ThrowIfNull(read);
        return extensions.Register(DataRegistryName, id, new BlockDataRegistration(read));
    }
}
