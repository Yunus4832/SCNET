namespace Game.Network;

public class ChannelFrameBuilder : IChannelFrameBuilder
{
    private readonly ChannelState[] _channelStates;

    public ChannelFrameBuilder(ChannelState[] channelStates)
    {
        _channelStates = channelStates;
    }

    public void BeginFrame()
    {
        for (var i = 0; i < _channelStates.Length; i++)
        {
            _channelStates[i].Writer.Clear();
        }
    }

    public void WriteSync(INetworkSync sync, ushort entityId)
    {
        if (!sync.IsDirty)
        {
            return;
        }

        var channelIndex = (int)sync.Channel;
        var writer = _channelStates[channelIndex].Writer;

        writer.Write(entityId);
        sync.WriteDirtyState(writer);

        _channelStates[channelIndex].MarkPending();
    }

    public void WriteMessage(NetworkChannel channel, INetworkMessage message)
    {
        var channelIndex = (int)channel;
        var writer = _channelStates[channelIndex].Writer;

        message.Write(writer);
        _channelStates[channelIndex].MarkPending();
    }

    public byte[] BuildChannel(NetworkChannel channel)
    {
        var channelIndex = (int)channel;
        return _channelStates[channelIndex].GetFrameData();
    }
}
