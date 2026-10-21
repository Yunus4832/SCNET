namespace ServerLoadTool;

public sealed record LoadWorldInfo(string Name, int Seed, string Mode, ushort MaximumPlayers)
{
    // Creative is represented by 0 in game metadata.
    private const byte _creativeMode = 0;

    public static LoadWorldInfo Read(WorldMetadata world)
    {
        if (world.Mode != _creativeMode)
        {
            throw new InvalidOperationException(
                "Load clients require a Creative test world; other game modes are not supported."
            );
        }

        return new LoadWorldInfo(world.Name, world.Seed, "Creative", world.MaximumPlayers);
    }
}
