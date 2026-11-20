using Engine.Network;

using Game.Localization;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Presentation;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class ConnectionReasonLocalizationTest
{
    [Theory]
    [InlineData("zh-CN", "在线人数达到最大，拒绝加入")]
    [InlineData("en-US", "The server is full")]
    [InlineData("pt-PT", "O servidor está cheio")]
    [InlineData("ru-RU", "Сервер заполнен")]
    public void RemoteCloseAndRejectionUseReceivingLocale(string culture, string expected)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        var previous = ClientLanguage.Catalog;
        var reason = ConnectionMessages.Create("ServerFull", "The server is full");
        var payload = NetNode.EncodeDisconnectReason(reason);
        Assert.InRange(payload.Length, 1, 256);
        try
        {
            ClientLanguage.Publish(runtime.Content.CreateLanguageCatalog(culture));
            foreach (var transportReason in new[]
                         { NetworkDisconnectReason.Rejected, NetworkDisconnectReason.RemoteClose })
            {
                var received = NetNode.DecodeDisconnectReason(new NetworkDisconnectInfo(transportReason, payload, 0));
                Assert.Equal(reason, received.Message);
                Assert.Equal(transportReason, received.TransportReason);
                Assert.Equal(expected, DisconnectReasonText.Format(received));
            }
        }
        finally { ClientLanguage.Publish(previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedDisconnectDetailUsesABoundedLocalizedReason(bool nested)
    {
        var detail = new string('x', 20000);
        var reason = nested
            ? ConnectionMessages.Create("MessageDeliveryFailed", "Message delivery failed: {0}", detail)
            : LocalizedText.Literal(detail);
        var payload = NetNode.EncodeDisconnectReason(reason);
        Assert.InRange(payload.Length, 1, 256);
        var received =
            NetNode.DecodeDisconnectReason(new NetworkDisconnectInfo(NetworkDisconnectReason.RemoteClose, payload, 0));
        Assert.Equal("Connection", received.Message.Section);
        Assert.Equal("Closed", received.Message.Key);
    }
}
