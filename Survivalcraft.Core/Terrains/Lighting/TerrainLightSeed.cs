namespace Game.Terrains.Lighting;

/// <summary>Immutable direct light and transmission inputs captured from one content revision.</summary>
public sealed class TerrainLightSeed
{
    private readonly byte[]?[] _transmission = new byte[TerrainChunk.SlicesCount][];

    private readonly byte[] _uniform = new byte[TerrainChunk.SlicesCount];

    public TerrainLightSnapshot Light { get; }

    public int StoredTransmissionBytes => _uniform.Length + _transmission.Sum(section => section?.Length ?? 0);

    internal TerrainLightSeed(TerrainLightSnapshot light, ReadOnlySpan<byte> transmission)
    {
        if (transmission.Length != 65536)
        {
            throw new ArgumentException("A chunk requires 65536 transmission values.", nameof(transmission));
        }

        Light = light;
        Span<byte> section = stackalloc byte[TerrainLightSection.CellCount];
        for (var slice = 0; slice < _transmission.Length; slice++)
        {
            for (var column = 0; column < 256; column++)
            {
                transmission.Slice(column * 256 + slice * 16, 16).CopyTo(section.Slice(column * 16, 16));
            }

            if (section.IndexOfAnyExcept(section[0]) < 0) { _uniform[slice] = section[0]; }
            else { _transmission[slice] = section.ToArray(); }
        }
    }

    public void CopyTransmissionTo(Span<byte> cells)
    {
        if (cells.Length < 65536)
        {
            throw new ArgumentException("A chunk requires 65536 transmission values.", nameof(cells));
        }

        Span<byte> section = stackalloc byte[TerrainLightSection.CellCount];
        for (var slice = 0; slice < _transmission.Length; slice++)
        {
            var values = _transmission[slice];
            if (values == null) { section.Fill(_uniform[slice]); }
            else { values.AsSpan().CopyTo(section); }

            for (var column = 0; column < 256; column++)
            {
                section.Slice(column * 16, 16).CopyTo(cells.Slice(column * 256 + slice * 16, 16));
            }
        }
    }
}
