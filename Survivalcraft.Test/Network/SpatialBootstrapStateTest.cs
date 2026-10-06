using System.Reflection;

using Engine.Core;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Network;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class SpatialBootstrapStateTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedBootstrapRestoresPreviousSaveMode(bool previousMode)
    {
        var project = new Project { SendToClientMode = previousMode };

        // 未初始化的项目在保存模板时失败，覆盖快照构造的异常路径。
        Assert.Throws<InvalidOperationException>(() => CommonLib.GetNowProject(project));

        Assert.Equal(previousMode, project.SendToClientMode);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void NetworkBootstrapOmitsBlocksButRetainsItemDefinitions(bool network, int blockCount)
    {
        var behavior = new SubsystemMemoryBankBlockBehavior();
        Attach(behavior, network);
        var data = new MemoryBankData();
        behavior.SetBlockData(new Point3(1, 2, 3), data);
        behavior.ItemsData[7] = data;
        var values = new ValuesDictionary();

        behavior.Save(values);

        Assert.Equal(blockCount, values.GetValue<ValuesDictionary>("Blocks").Count);
        Assert.Single(values.GetValue<ValuesDictionary>("Items"));
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "1,2,3,0.5;")]
    public void NetworkBootstrapOmitsVoltageStateWithoutChangingDiskSave(bool network, string expected)
    {
        var electricity = new SubsystemElectricity();
        Attach(electricity, network);
        electricity.WritePersistentVoltage(new Point3(1, 2, 3), 0.5f);
        var values = new ValuesDictionary();

        electricity.Save(values);

        Assert.Equal(expected, values.GetValue<string>("VoltagesByCell"));
        Assert.Equal(0, values.GetValue<int>("Step"));
    }

    private static void Attach(Subsystem subsystem, bool network)
    {
        typeof(Subsystem).GetProperty(nameof(Subsystem.Project))!
            .SetValue(subsystem, new Project { SendToClientMode = network });
        typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subsystem, true);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void NetworkBootstrapOmitsSignTextWithoutChangingDiskSave(bool network, int count)
    {
        var behavior = new SubsystemSignBlockBehavior();
        Attach(behavior, network);
        typeof(SubsystemSignBlockBehavior).GetField("_subsystemGameInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(behavior, new SubsystemGameInfo { WorldSettings = new WorldSettings { KeywordBlocking = "" } });
        behavior.SetSignData(new Point3(1, 2, 3), ["one", "two", "three", "four"],
            [Color.White, Color.White, Color.White, Color.White], "");
        var values = new ValuesDictionary();

        behavior.Save(values);

        Assert.Equal(count, values.GetValue<ValuesDictionary>("Texts").Count);
    }
}
