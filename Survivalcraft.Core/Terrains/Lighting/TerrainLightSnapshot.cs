namespace Game.Terrains.Lighting;

/// <summary>Immutable spatial light data whose sections can be shared across publications.</summary>
public sealed class TerrainLightSnapshot
{
    private readonly TerrainLightSection[] _sections;

    public int StoredBytes => _sections.Sum(section => section.StoredBytes);

    public TerrainLightSnapshot(TerrainLightChannels uniform)
    {
        _sections = new TerrainLightSection[TerrainChunk.SlicesCount];
        Array.Fill(_sections, new TerrainLightSection(uniform));
    }

    private TerrainLightSnapshot(TerrainLightSection[] sections)
    {
        _sections = sections;
    }

    public static TerrainLightSnapshot Capture(ReadOnlySpan<byte> cells, TerrainLightSnapshot? previous = null)
    {
        if (cells.Length != 65536)
        {
            throw new ArgumentException("A chunk requires 65536 light values.", nameof(cells));
        }

        TerrainLightSection[]? sections = null;
        Span<byte> section = stackalloc byte[TerrainLightSection.CellCount];
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            for (var column = 0; column < 256; column++)
            {
                cells.Slice(column * 256 + slice * 16, 16).CopyTo(section.Slice(column * 16, 16));
            }

            if (previous?._sections[slice].HasSameBytes(section) == true) { continue; }

            sections ??= previous == null
                ? new TerrainLightSection[TerrainChunk.SlicesCount]
                : (TerrainLightSection[])previous._sections.Clone();
            sections[slice] = TerrainLightSection.Capture(section);
        }

        return sections == null ? previous! : new TerrainLightSnapshot(sections);
    }

    public void CopyTo(Span<byte> cells)
    {
        if (cells.Length < 65536)
        {
            throw new ArgumentException("A chunk requires 65536 light values.", nameof(cells));
        }

        Span<byte> section = stackalloc byte[TerrainLightSection.CellCount];
        for (var slice = 0; slice < _sections.Length; slice++)
        {
            _sections[slice].CopyTo(section);
            for (var column = 0; column < 256; column++)
            {
                section.Slice(column * 16, 16).CopyTo(cells.Slice(column * 256 + slice * 16, 16));
            }
        }
    }

    public bool HasSameValues(TerrainLightSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        for (var i = 0; i < _sections.Length; i++)
        {
            if (!_sections[i].HasSameValues(other._sections[i])) { return false; }
        }

        return true;
    }

    public bool HasSameBoundary(TerrainLightSnapshot other, int face)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)face, 3u);
        for (var column = 0; column < 16; column++)
        {
            var x = face switch { 1 => 15, 3 => 0, _ => column };
            var z = face switch { 0 => 15, 2 => 0, _ => column };
            for (var y = 0; y < 256; y++)
            {
                if (Get(x, y, z) != other.Get(x, y, z)) { return false; }
            }
        }

        return true;
    }

    public TerrainLightSection GetSection(int index) => _sections[index];

    public TerrainLightChannels Get(int x, int y, int z)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)x, 16u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)y, 256u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)z, 16u);
        return _sections[y >> 4].Get((y & 15) + (x << 4) + (z << 8));
    }

    public TerrainLightSnapshot WithSection(int index, TerrainLightSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_sections.Length);
        var sections = (TerrainLightSection[])_sections.Clone();
        sections[index] = section;
        return new TerrainLightSnapshot(sections);
    }
}
