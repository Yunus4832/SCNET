namespace Game.Modding.Blocks;

internal static class BuiltInBlockRegistration
{
    internal static void Register(IModExtensions extensions)
    {
        foreach (var entry in BuiltInBlockRegistry.Entries)
        {
            extensions.Register(
                BlockExtensions.RegistryName,
                new ResourceId(BuiltInBlockIdentities.Namespace, entry.Path),
                new BlockRegistration(entry.Factory)
            );
        }
    }
}
