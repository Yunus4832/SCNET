namespace NetworkDamageTool;

public readonly record struct ImpairmentDecision(bool Drop, TimeSpan Delay);

public sealed class LinkImpairment(LinkImpairmentOptions options, int seed)
{
    private readonly Random _random = new(seed);

    private TimeSpan _nextTransmission;

    public ImpairmentDecision Decide(int byteCount, TimeSpan elapsed)
    {
        if (_random.NextDouble() < options.LossProbability)
        {
            return new ImpairmentDecision(true, TimeSpan.Zero);
        }

        var jitter = options.JitterMilliseconds == 0
            ? 0
            : _random.Next(-options.JitterMilliseconds, options.JitterMilliseconds + 1);
        var propagationDelay = TimeSpan.FromMilliseconds(
            Math.Max(0, options.LatencyMilliseconds + jitter));
        var sendAt = elapsed + propagationDelay;
        if (options.BandwidthKilobitsPerSecond <= 0)
        {
            return new ImpairmentDecision(false, sendAt - elapsed);
        }

        sendAt = sendAt > _nextTransmission ? sendAt : _nextTransmission;
        var serializationSeconds = byteCount * 8d / (options.BandwidthKilobitsPerSecond * 1000d);
        _nextTransmission = sendAt + TimeSpan.FromSeconds(serializationSeconds);

        return new ImpairmentDecision(false, sendAt - elapsed);
    }
}
