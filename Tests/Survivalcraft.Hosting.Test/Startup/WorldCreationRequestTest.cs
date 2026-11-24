using Game.Admission;
using Game.Protocol.Connection;
using Game.Startup;
using Game.World;

namespace Survivalcraft.Hosting.Test.Startup;

/// <summary>验证切换模组环境时，世界创建草稿随会话完整往返。</summary>
public sealed class WorldCreationRequestTest
{
    [Fact]
    public void SessionClonePreservesCreationSettingsHostingAndAdmission()
    {
        var admission = ServerAdmissionPolicy.WithPassword("creation-password");
        var original = new SessionInfo
        {
            Target = SessionTarget.HostedWorld,
            World = "worlds:new-world",
            CreateWorldIfMissing = true,
            WorldCreation = new WorldCreationRequest(
                new WorldSettings { Name = "SelectedWorld", Seed = "chosen-seed", GameMode = GameMode.Creative },
                new WorldHostingSettings { RunServer = true },
                admission
            )
        };

        var cloned = SessionInfoSerializer.Clone(original);
        var restored = new SessionInfo();
        SessionInfoSerializer.Populate(restored, SessionInfoSerializer.Serialize(cloned));

        Assert.NotSame(original, restored);
        Assert.Equal(SessionTarget.HostedWorld, restored.Target);
        Assert.Equal("worlds:new-world", restored.World);
        Assert.True(restored.CreateWorldIfMissing);
        var creation = Assert.IsType<WorldCreationRequest>(restored.WorldCreation);
        Assert.Equal("SelectedWorld", creation.Settings.Name);
        Assert.Equal("chosen-seed", creation.Settings.Seed);
        Assert.Equal(GameMode.Creative, creation.Settings.GameMode);
        Assert.True(creation.Hosting.RunServer);
        Assert.Equal(ServerAdmissionMode.Password, creation.Admission.Mode);
        Assert.Equal(admission.Fingerprint(), creation.Admission.Fingerprint());
        Assert.NotSame(original.WorldCreation.Settings, creation.Settings);
    }
}
