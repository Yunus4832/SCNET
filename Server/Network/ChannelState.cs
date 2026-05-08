namespace Game.Network;

public class ChannelState
{
    public NetworkChannel Channel { get; }
    public bool Reliable { get; }
    public bool Compress { get; }
    public bool FragmentLarge { get; }
    public int MaxFragmentSize { get; }
    public double MinSendInterval { get; }

    private double _lastSendTime = -1.0;
    private bool _hasPendingData;

    public NetworkWriter Writer { get; } = new();

    private ChannelState(
        NetworkChannel channel,
        bool reliable,
        bool compress,
        bool fragmentLarge,
        int maxFragmentSize,
        double minSendInterval)
    {
        Channel = channel;
        Reliable = reliable;
        Compress = compress;
        FragmentLarge = fragmentLarge;
        MaxFragmentSize = maxFragmentSize;
        MinSendInterval = minSendInterval;
    }

    public static ChannelState Create(NetworkChannel channel)
    {
        return channel switch
        {
            NetworkChannel.Control => new ChannelState(channel, true, false, false, 0, 0.0),
            NetworkChannel.Input => new ChannelState(channel, false, false, false, 0, 0.0),
            NetworkChannel.Entity => new ChannelState(channel, true, true, false, 0, 0.0),
            NetworkChannel.Subsystem => new ChannelState(channel, true, true, false, 0, 15.0),
            NetworkChannel.Terrain => new ChannelState(channel, true, true, true, 1200, 0.0),
            NetworkChannel.Event => new ChannelState(channel, true, true, false, 0, 0.0),
            NetworkChannel.Mod => new ChannelState(channel, true, true, false, 0, 0.0),
            _ => new ChannelState(channel, true, false, false, 0, 0.0)
        };
    }

    public bool ShouldSend(double currentTime)
    {
        if (!_hasPendingData)
        {
            return false;
        }

        if (_lastSendTime < 0)
        {
            return true;
        }

        return currentTime - _lastSendTime >= MinSendInterval;
    }

    public void MarkSent(double currentTime)
    {
        _lastSendTime = currentTime;
        _hasPendingData = false;
    }

    public void MarkPending()
    {
        _hasPendingData = true;
    }

    public byte[] GetFrameData()
    {
        var data = Writer.ToArray();
        Writer.Clear();
        return data;
    }

    public bool HasPendingData => _hasPendingData;
}
