namespace Game.Network.Chunks;

public sealed class ChunkSendBudget
{
    public const int MaximumFragmentsPerUpdate = 64;

    private double _lastUpdate = double.NaN;

    private double _chunks;

    private double _bytes;

    private int _remainingFragments;

    public void Advance(double now, int chunksPerSecond, int bytesPerSecond)
    {
        if (!double.IsFinite(now)) { throw new ArgumentOutOfRangeException(nameof(now)); }

        if (!double.IsNaN(_lastUpdate) && now < _lastUpdate)
        {
            _remainingFragments = 0;
            return;
        }

        var newFrame = double.IsNaN(_lastUpdate) || now > _lastUpdate;
        var chunkRate = Math.Max(1, chunksPerSecond);
        var byteRate = Math.Max(1, bytesPerSecond);
        if (double.IsNaN(_lastUpdate))
        {
            _chunks = 1;
            _bytes = ProtocolLimits.TerrainFragmentPayloadSize;
        }
        else
        {
            var elapsed = Math.Max(0, now - _lastUpdate);
            _chunks = Math.Min(chunkRate, _chunks + elapsed * chunkRate);
            _bytes = Math.Min(
                Math.Max(byteRate, ProtocolLimits.TerrainFragmentPayloadSize),
                _bytes + elapsed * byteRate
            );
        }

        _lastUpdate = now;
        if (newFrame) { _remainingFragments = MaximumFragmentsPerUpdate; }
    }

    public bool CanStart => _chunks >= 1;

    public bool TrySpend(int bytes, bool starting)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        if (_remainingFragments <= 0 || _bytes < bytes || (starting && !CanStart)) { return false; }

        if (starting) { _chunks--; }

        _bytes -= bytes;
        _remainingFragments--;
        return true;
    }
}
