namespace Game.Modding.Blocks;

public sealed record BlockRegistration(Func<Block> Factory);
