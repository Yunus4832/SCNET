using System.Runtime.InteropServices;

namespace Game.Terrains.Lighting;

/// <summary>单个 16×16×16 分段的不可变光照发布数据。</summary>
public sealed class TerrainLightSection
{
    public const int CellCount = 4096;

    private readonly byte[]? _values;

    private readonly byte _uniform;

    public int StoredBytes => _values?.Length ?? 0;

    /// <summary>比较不同发布中的等效光照值，使用方无需保留单元格存储。</summary>
    public ulong Fingerprint { get; }

    public TerrainLightSection(TerrainLightChannels uniform)
    {
        _uniform = uniform.Packed;
        Fingerprint = (ulong)_uniform << 1;
    }

    private TerrainLightSection(byte[] values)
    {
        _values = values;
        var fingerprint = 14695981039346656037UL;
        foreach (var word in MemoryMarshal.Cast<byte, ulong>(values.AsSpan()))
        {
            fingerprint = (fingerprint ^ word) * 1099511628211UL;
        }

        Fingerprint = fingerprint | 1;
    }

    public static TerrainLightSection Capture(ReadOnlySpan<byte> values)
    {
        if (values.Length != CellCount)
        {
            throw new ArgumentException("A light section requires 4096 cells.", nameof(values));
        }

        return values.IndexOfAnyExcept(values[0]) < 0
            ? new TerrainLightSection(TerrainLightChannels.FromPacked(values[0]))
            : new TerrainLightSection(values.ToArray());
    }

    public TerrainLightChannels Get(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)CellCount);
        return TerrainLightChannels.FromPacked(_values == null ? _uniform : _values[index]);
    }

    internal bool HasSameBytes(ReadOnlySpan<byte> values)
    {
        return values.Length == CellCount &&
               (_values == null
                   ? values.IndexOfAnyExcept(_uniform) < 0
                   : values.SequenceEqual(_values));
    }

    public bool HasSameValues(TerrainLightSection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other)) { return true; }

        if (_values == null)
        {
            return other._values == null
                ? _uniform == other._uniform
                : other._values.AsSpan().IndexOfAnyExcept(_uniform) < 0;
        }

        return other._values == null
            ? _values.AsSpan().IndexOfAnyExcept(other._uniform) < 0
            : _values.AsSpan().SequenceEqual(other._values);
    }

    internal bool HasSameColumn(TerrainLightSection other, int start)
    {
        if (_values == null)
        {
            return other._values == null
                ? _uniform == other._uniform
                : other._values.AsSpan(start, 16).IndexOfAnyExcept(_uniform) < 0;
        }

        return other._values == null
            ? _values.AsSpan(start, 16).IndexOfAnyExcept(other._uniform) < 0
            : _values.AsSpan(start, 16).SequenceEqual(other._values.AsSpan(start, 16));
    }

    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < CellCount)
        {
            throw new ArgumentException("A light section requires space for 4096 cells.", nameof(destination));
        }

        if (_values == null) { destination[..CellCount].Fill(_uniform); }
        else { _values.AsSpan().CopyTo(destination); }
    }
}
