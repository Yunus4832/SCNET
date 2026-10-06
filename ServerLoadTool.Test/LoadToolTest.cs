using System.Text.Json;

using Engine.Core;

using EntitySystem.TemplatesDatabase;

using Game;

using Game.Network.Packages;

using LiteNetLib.Utils;

namespace ServerLoadTool.Test;

public class LoadToolTest
{
    [Fact]
    public void InterestMatchesTerrainAllocationAnchorAndMovementThreshold()
    {
        Assert.True(LoadInterest.ShouldMove(null, Vector2.Zero));
        Assert.False(LoadInterest.ShouldMove(Vector2.Zero, new Vector2(8, 0)));
        Assert.True(LoadInterest.ShouldMove(Vector2.Zero, new Vector2(9, 0)));
        var center = new Vector2(-158.5f, 51.5f);
        var chunks = LoadInterest.Chunks(center, 32);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.True(Vector2.DistanceSquared(center,
            new Vector2(chunk.X * 16 + 8, chunk.Y * 16 + 8)) <= 32 * 32));
    }

    [Fact]
    public void PositionReportsDoNotTraverseEngineSwizzles()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(LoadPosition.From(new Vector3(1, 2, 3))));
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
        Assert.Equal(2, json.RootElement.GetProperty("Y").GetSingle());
        Assert.Null(LoadPosition.From(null));
    }

    [Fact]
    public void PlayerBodyCodecIncludesConnectionIdentity()
    {
        var package = new ComponentPlayerPackage
        {
            Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
            Position = new Vector3(10, 65, -20),
            Velocity = Vector3.Zero,
            PackageChangeFlag = ComponentPlayerPackage.ChangFlag.PositionChange | ComponentPlayerPackage.ChangFlag.VelocityChange
        };
        var encoded = LoadWire.Encode(package, 7);
        var decoded = Assert.IsType<ComponentPlayerPackage>(Assert.Single(LoadWire.Decode(new NetDataReader(encoded.CopyData()))));
        Assert.Equal(7, decoded.FromPlayerId);
        Assert.Equal(package.Position, decoded.Position);
        Assert.Equal(package.PackageChangeFlag, decoded.PackageChangeFlag);
    }

    [Fact]
    public void ChunkRequestCodecUsesCurrentAllocationContract()
    {
        var package = new SubsystemTerrainPackage([new(new(new Point2(-2, 4), 7), 99)]);
        var decoded = Assert.IsType<SubsystemTerrainPackage>(Assert.Single(LoadWire.Decode(new NetDataReader(LoadWire.Encode(package).CopyData()))));
        Assert.Equal(package.ChunkRequests, decoded.ChunkRequests);
    }

    [Theory]
    [InlineData("--clients", "0")]
    [InlineData("--clients", "201")]
    [InlineData("--duration", "NaN")]
    [InlineData("--speed", "Infinity")]
    [InlineData("--workload", "agent")]
    [InlineData("--port", "0")]
    public void InvalidOptionsCannotStartLoad(string name, string value)
    {
        Assert.Throws<ArgumentException>(() => LoadOptions.Parse(["--output", "unused", name, value]));
    }

    [Fact]
    public void OutputIsRequiredAndUnknownOptionsFail()
    {
        Assert.Throws<ArgumentException>(() => LoadOptions.Parse([]));
        Assert.Throws<ArgumentException>(() => LoadOptions.Parse(["--output", "unused", "--unknown", "1"]));
    }

    [Fact]
    public void DeterministicMovementStartsAtSpawnAndStaysBoundedForSharedWorkload()
    {
        var origin = new Vector3(100, 65, 200);
        var options = new LoadOptions { Workload = "shared" };
        Assert.Equal(origin, LoadMotion.Position(origin, 0, 0, options));
        Assert.Equal(origin, LoadMotion.Position(origin, 8, 0, options));
        Assert.Equal(origin + new Vector3(16, 0, 0), LoadMotion.Position(origin, 4, 0, options));
        var explore = options with { Workload = "explore" };
        Assert.NotEqual(LoadMotion.Position(origin, 10, 0, explore), LoadMotion.Position(origin, 10, 1, explore));
    }

    [Fact]
    public void PercentilesUseNearestRankAndMissingSamplesStayMissing()
    {
        Assert.Null(LoadRun.Percentile([], 0.95));
        Assert.Equal(19, LoadRun.Percentile(Enumerable.Range(1, 20).Select(value => (double)value), 0.95));
    }

    [Theory]
    [InlineData(GameMode.Creative, true)]
    [InlineData(GameMode.Survival, false)]
    public void BootstrapWorldMetadataPreventsUnsupportedGameModes(GameMode mode, bool supported)
    {
        var info = new ValuesDictionary
        {
            { "WorldName", "LoadWorld" }, { "WorldSeed", 123 }, { "GameMode", mode }, { "MaxOnlinePlayerCount", (ushort)20 }
        };
        var project = new ValuesDictionary { { "Subsystems", new ValuesDictionary { { "GameInfo", info } } } };
        if (supported)
        {
            Assert.Equal(new LoadWorldInfo("LoadWorld", 123, "Creative", 20), LoadWorldInfo.Read(project.ToMessagePack()));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => LoadWorldInfo.Read(project.ToMessagePack()));
        }
    }
}
