using LiteNetLib;

namespace Engine.Network.Test;

public sealed class LiteNetLoggingTest
{
    [Fact]
    public void UnreachableWarningsAreConciseLimitedAndResumeAfterTheInterval()
    {
        var clock = new ManualClock();
        var messages = new List<(NetworkLogLevel Level, string Message)>();
        var logger = new LiteNetLog((level, message) => messages.Add((level, message)), clock);
        const string unreachable =
            "[S]System.Net.Sockets.SocketException (101): Network is unreachable\n   at Socket.SendTo()";
        for (var i = 0; i < 60; i++)
        {
            logger.WriteNet(NetLogLevel.Error, unreachable);
        }

        logger.WriteNet(NetLogLevel.Error, "Unexpected failure: {0}", "diagnostic detail");
        Assert.Equal(2, messages.Count);
        Assert.Equal(NetworkLogLevel.Warning, messages[0].Level);
        Assert.Contains("Network is unreachable", messages[0].Message);
        Assert.DoesNotContain("Socket.SendTo", messages[0].Message);
        Assert.Equal(NetworkLogLevel.Error, messages[1].Level);
        Assert.Contains("diagnostic detail", messages[1].Message);
        clock.Elapsed = TimeSpan.FromSeconds(29);
        logger.WriteNet(NetLogLevel.Error, unreachable);
        Assert.Equal(2, messages.Count);
        clock.Elapsed = TimeSpan.FromSeconds(30);
        logger.WriteNet(NetLogLevel.Error, unreachable);
        Assert.Equal(3, messages.Count);
    }

    [Theory]
    [InlineData(NetLogLevel.Trace, NetworkLogLevel.Debug)]
    [InlineData(NetLogLevel.Info, NetworkLogLevel.Information)]
    [InlineData(NetLogLevel.Warning, NetworkLogLevel.Warning)]
    [InlineData(NetLogLevel.Error, NetworkLogLevel.Error)]
    public void MapsLevelsAndPreservesArgumentsWhenTheFormatIsInvalid(NetLogLevel input, NetworkLogLevel expected)
    {
        var messages = new List<(NetworkLogLevel Level, string Message)>();
        var logger = new LiteNetLog((level, message) => messages.Add((level, message)));
        logger.WriteNet(input, "Invalid {format", "diagnostic detail");
        var item = Assert.Single(messages);
        Assert.Equal(expected, item.Level);
        Assert.Contains("Invalid {format", item.Message);
        Assert.Contains("diagnostic detail", item.Message);
    }

    private sealed class ManualClock : TimeProvider
    {
        public TimeSpan Elapsed { get; set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Elapsed.Ticks;
    }
}
