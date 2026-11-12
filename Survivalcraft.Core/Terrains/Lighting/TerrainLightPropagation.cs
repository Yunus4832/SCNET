using System.Buffers;

namespace Game.Terrains.Lighting;

/// <summary>Private, bounded flood fill which writes only the owning chunk's result.</summary>
internal sealed class TerrainLightPropagation : IDisposable
{
    private const int _cellCount = 65536;

    private byte[] _transmission = null!;

    private byte[] _values = null!;

    private byte[] _queued = null!;

    private int[]? _pending;

    private int _head;

    private int _tail;

    private int _count;

    private bool _disposed;

    public TerrainLightPropagation(
        TerrainLightSeed seed,
        TerrainLightSnapshot?[] neighbors,
        CancellationToken cancellation = default
    )
    {
        try
        {
            _transmission = ArrayPool<byte>.Shared.Rent(_cellCount);
            _values = ArrayPool<byte>.Shared.Rent(_cellCount);
            _queued = ArrayPool<byte>.Shared.Rent(_cellCount);
            Array.Clear(_queued, 0, _cellCount);
            Initialize(seed, neighbors, cancellation);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Initialize(TerrainLightSeed seed, TerrainLightSnapshot?[] neighbors, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        seed.CopyTransmissionTo(_transmission);
        seed.Light.CopyTo(_values);
        for (var face = 0; face < 4; face++)
        {
            cancellation.ThrowIfCancellationRequested();
            var neighbor = neighbors[face];
            if (neighbor == null) { continue; }

            for (var column = 0; column < 16; column++)
            {
                var x = face switch { 1 => 15, 3 => 0, _ => column };
                var z = face switch { 0 => 15, 2 => 0, _ => column };
                var neighborX = face switch { 1 => 0, 3 => 15, _ => column };
                var neighborZ = face switch { 0 => 0, 2 => 15, _ => column };
                for (var y = 0; y < 256; y++)
                {
                    Increase(TerrainChunk.CalculateCellIndex(x, y, z), neighbor.Get(neighborX, y, neighborZ));
                }
            }
        }

        for (var index = 0; index < _cellCount; index++)
        {
            if ((index & 255) == 0) { cancellation.ThrowIfCancellationRequested(); }

            if (_values[index] == 0) { continue; }

            var source = TerrainLightChannels.FromPacked(_values[index]);
            var x = (index >> 8) & 15;
            var y = index & 255;
            var z = index >> 12;
            if ((x > 0 && CanIncrease(index - 256, source)) ||
                (x < 15 && CanIncrease(index + 256, source)) ||
                (y > 0 && CanIncrease(index - 1, source)) ||
                (y < 255 && CanIncrease(index + 1, source)) ||
                (z > 0 && CanIncrease(index - 4096, source)) ||
                (z < 15 && CanIncrease(index + 4096, source)))
            {
                Enqueue(index);
            }
        }
    }

    public bool Advance(int maximumSources, CancellationToken cancellation = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSources);
        cancellation.ThrowIfCancellationRequested();
        for (var processed = 0; processed < maximumSources && _count != 0; processed++)
        {
            var index = _pending![_head];
            _head = (_head + 1) & (_cellCount - 1);
            _count--;
            if ((processed & 255) == 0) { cancellation.ThrowIfCancellationRequested(); }

            _queued[index] = 0;
            var source = TerrainLightChannels.FromPacked(_values[index]);
            var x = (index >> 8) & 15;
            var y = index & 255;
            var z = index >> 12;
            if (x > 0) { Increase(index - 256, source); }

            if (x < 15) { Increase(index + 256, source); }

            if (y > 0) { Increase(index - 1, source); }

            if (y < 255) { Increase(index + 1, source); }

            if (z > 0) { Increase(index - 4096, source); }

            if (z < 15) { Increase(index + 4096, source); }
        }

        return _count == 0;
    }

    public TerrainLightSnapshot Capture(TerrainLightSnapshot? previous = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TerrainLightSnapshot.Capture(_values.AsSpan(0, _cellCount), previous);
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_transmission != null) { ArrayPool<byte>.Shared.Return(_transmission); }

        if (_values != null) { ArrayPool<byte>.Shared.Return(_values); }

        if (_queued != null) { ArrayPool<byte>.Shared.Return(_queued); }

        if (_pending != null) { ArrayPool<int>.Shared.Return(_pending); }
    }

    private bool CanIncrease(int index, TerrainLightChannels source)
    {
        var current = TerrainLightChannels.FromPacked(_values[index]);
        var loss = _transmission[index];
        return loss < 255 && (source.Sky - loss > current.Sky || source.Block - loss > current.Block);
    }

    private void Increase(int index, TerrainLightChannels source)
    {
        if (!CanIncrease(index, source)) { return; }

        var current = TerrainLightChannels.FromPacked(_values[index]);
        _values[index] = current.Max(source.Attenuate(_transmission[index])).Packed;
        Enqueue(index);
    }

    private void Enqueue(int index)
    {
        if (_queued[index] != 0) { return; }

        _queued[index] = 1;
        _pending ??= ArrayPool<int>.Shared.Rent(_cellCount);
        _pending[_tail] = index;
        _tail = (_tail + 1) & (_cellCount - 1);
        _count++;
    }
}
