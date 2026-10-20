using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Network;
using Game.Servers;

namespace Survivalcraft.Test.Network;

public sealed class ProtocolEnumTest
{
    [Fact]
    public void GameConceptsBelongToTheGameAssembly()
    {
        var assembly = typeof(PlayerData).Assembly;
        foreach (var type in new[] { typeof(GameMode), typeof(PlayerClass), typeof(Season), typeof(DragMode) })
        {
            Assert.Equal("Game", type.Namespace);
            Assert.Equal(assembly, type.Assembly);
            Assert.Null(typeof(GameProtocol).Assembly.GetType(type.FullName!));
        }
    }

    [Fact]
    public void GameEnumsRoundTripThroughWorldXml()
    {
        var source = new ValuesDictionary();
        source.SetValue("Mode", GameMode.Survival);
        source.SetValue("Class", PlayerClass.Female);
        source.SetValue("Season", Season.Winter);
        source.SetValue("Drag", DragMode.SingleItem);
        var xml = new XElement("Values");
        source.Save(xml);
        var restored = new ValuesDictionary();
        restored.ApplyOverrides(xml);
        Assert.Equal(GameMode.Survival, restored.GetValue<GameMode>("Mode"));
        Assert.Equal(PlayerClass.Female, restored.GetValue<PlayerClass>("Class"));
        Assert.Equal(Season.Winter, restored.GetValue<Season>("Season"));
        Assert.Equal(DragMode.SingleItem, restored.GetValue<DragMode>("Drag"));
    }

    [Fact]
    public void GameEnumValuesAreValidatedAtTheProtocolBoundary()
    {
        Assert.Equal((byte)2, ProtocolEnum.Encode(GameMode.Survival));
        Assert.Equal((byte)1, ProtocolEnum.Encode(PlayerClass.Female));
        Assert.Equal((byte)2, ProtocolEnum.Encode(Season.Winter));
        Assert.Equal((byte)1, ProtocolEnum.Encode(DragMode.SingleItem));
        RejectUnknown<GameMode>();
        RejectUnknown<PlayerClass>();
        RejectUnknown<Season>();
        RejectUnknown<DragMode>();
    }

    [Theory]
    [InlineData((byte)255, (byte)0)]
    [InlineData((byte)0, (byte)255)]
    public void UnsupportedServerStateIsRejectedBeforeBecomingRuntimeStatus(byte mode, byte season)
    {
        var package = new ServerInfoPackage { GameMode = mode, Season = season };
        Assert.Throws<InvalidDataException>(() => ServerRuntimeStatus.FromPackage(package, 1));
    }

    [Fact]
    public void ServerStatusRestoresGameEnumsFromBytes()
    {
        var package = new ServerInfoPackage
        {
            GameMode = ProtocolEnum.Encode(GameMode.Survival),
            Season = ProtocolEnum.Encode(Season.Winter)
        };
        var status = ServerRuntimeStatus.FromPackage(package, 1);
        Assert.Equal(GameMode.Survival, status.GameMode);
        Assert.Equal(Season.Winter, status.Season);
    }

    private static void RejectUnknown<TEnum>() where TEnum : struct, Enum
    {
        Assert.False(ProtocolEnum.TryDecode<TEnum>(byte.MaxValue, out _));
        Assert.Throws<InvalidDataException>(() => ProtocolEnum.Decode<TEnum>(byte.MaxValue));
        var invalid = (TEnum)Enum.ToObject(typeof(TEnum), byte.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProtocolEnum.Encode(invalid));
    }
}
