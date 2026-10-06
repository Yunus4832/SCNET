using System.Diagnostics;

namespace Game.Network;

public readonly record struct NetworkChannelSendStatistics(long Batches, long EncodedBytes);

public readonly record struct NetworkPackageSendStatistics(
    Type PackageType, NetworkChannel Channel, long Deliveries, long PayloadBytes);

public sealed class NetworkSendStatistics
{
    private readonly object _gate = new();

    private readonly Dictionary<NetworkChannel, NetworkChannelSendStatistics> _channels = [];

    private readonly Dictionary<(Type Type, NetworkChannel Channel), (long Deliveries, long PayloadBytes)> _packages = [];

    private readonly Dictionary<(Type Type, NetworkChannel Channel), NetworkPackageFanoutStatistics> _fanout = [];

    private long _routingEvaluations;
    private long _selectedPackages;
    private long _routingTicks;
    private long _maximumRoutingTicks;

    private long _encodingBatches;
    private long _encodedPackages;
    private long _rawEncodingBytes;
    private long _encodedBytes;
    private long _serializationTicks;
    private long _frameEncodingTicks;

    public void RecordFanout(Type packageType, NetworkChannel channel, int recipients)
    {
        ArgumentNullException.ThrowIfNull(packageType);
        ArgumentOutOfRangeException.ThrowIfNegative(recipients);
        lock (_gate)
        {
            var key = (packageType, channel);
            _fanout.TryGetValue(key, out var previous);
            _fanout[key] = new NetworkPackageFanoutStatistics(packageType, channel,
                previous.Messages + 1, previous.RecipientDeliveries + recipients,
                Math.Max(previous.MaximumFanout, recipients));
        }
    }

    public IReadOnlyList<NetworkPackageFanoutStatistics> GetFanout()
    {
        lock (_gate)
        {
            return _fanout.Values.ToArray();
        }
    }

    public void RecordEncoding(int packages, long rawBytes, int encodedBytes,
        long serializationTicks, long frameEncodingTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(packages);
        ArgumentOutOfRangeException.ThrowIfNegative(rawBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(encodedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(serializationTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(frameEncodingTicks);
        lock (_gate)
        {
            _encodingBatches++;
            _encodedPackages += packages;
            _rawEncodingBytes += rawBytes;
            _encodedBytes += encodedBytes;
            _serializationTicks += serializationTicks;
            _frameEncodingTicks += frameEncodingTicks;
        }
    }

    public NetworkEncodingStatistics GetEncoding()
    {
        lock (_gate)
        {
            return new NetworkEncodingStatistics(_encodingBatches, _encodedPackages,
                _rawEncodingBytes, _encodedBytes, _serializationTicks * 1000d / Stopwatch.Frequency,
                _frameEncodingTicks * 1000d / Stopwatch.Frequency);
        }
    }

    public void RecordRouting(int evaluations, int selectedPackages, long elapsedTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(selectedPackages);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedTicks);
        lock (_gate)
        {
            _routingEvaluations += evaluations;
            _selectedPackages += selectedPackages;
            _routingTicks += elapsedTicks;
            _maximumRoutingTicks = Math.Max(_maximumRoutingTicks, elapsedTicks);
        }
    }

    public NetworkRoutingStatistics GetRouting()
    {
        lock (_gate)
        {
            return new NetworkRoutingStatistics(_routingEvaluations, _selectedPackages,
                _routingTicks * 1000d / Stopwatch.Frequency,
                _maximumRoutingTicks * 1000d / Stopwatch.Frequency);
        }
    }

    public void Record(NetworkChannel channel, int encodedBytes,
        IEnumerable<(Type PackageType, int PayloadBytes)> packages)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(encodedBytes);
        lock (_gate)
        {
            _channels.TryGetValue(channel, out var previous);
            _channels[channel] = new NetworkChannelSendStatistics(
                previous.Batches + 1, previous.EncodedBytes + encodedBytes);
            foreach (var (type, payloadBytes) in packages)
            {
                var key = (type, channel);
                _packages.TryGetValue(key, out var count);
                _packages[key] = (count.Deliveries + 1, count.PayloadBytes + payloadBytes);
            }
        }
    }

    public IReadOnlyDictionary<NetworkChannel, NetworkChannelSendStatistics> GetChannels()
    {
        lock (_gate)
        {
            return new Dictionary<NetworkChannel, NetworkChannelSendStatistics>(_channels);
        }
    }

    public IReadOnlyList<NetworkPackageSendStatistics> GetPackages()
    {
        lock (_gate)
        {
            return _packages.Select(item => new NetworkPackageSendStatistics(
                item.Key.Type, item.Key.Channel, item.Value.Deliveries, item.Value.PayloadBytes)).ToArray();
        }
    }
}
