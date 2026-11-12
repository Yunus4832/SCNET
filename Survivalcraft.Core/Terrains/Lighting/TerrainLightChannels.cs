namespace Game.Terrains.Lighting;

/// <summary>Independent sky exposure and emitted light, each in the range zero through fifteen.</summary>
public readonly record struct TerrainLightChannels
{
    public byte Packed { get; }

    public int Sky => Packed >> 4;

    public int Block => Packed & 15;

    public TerrainLightChannels(int sky, int block)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)sky, 15u);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)block, 15u);
        Packed = (byte)((sky << 4) | block);
    }

    private TerrainLightChannels(byte packed)
    {
        Packed = packed;
    }

    public static TerrainLightChannels FromPacked(byte packed) => new(packed);

    public int GetEffectiveLight(TerrainLightEnvironment environment)
    {
        return Math.Max(Block, Math.Max(0, Sky - (15 - environment.SkyLightValue)));
    }

    public TerrainLightChannels Max(TerrainLightChannels other)
    {
        return new TerrainLightChannels(Math.Max(Sky, other.Sky), Math.Max(Block, other.Block));
    }

    public TerrainLightChannels Attenuate(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new TerrainLightChannels(Math.Max(0, Sky - amount), Math.Max(0, Block - amount));
    }
}
