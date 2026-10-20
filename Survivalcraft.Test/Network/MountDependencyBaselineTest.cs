using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Components;
using Game.Network;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class MountDependencyBaselineTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MissingMountedDependenciesAreQueuedOnceWithoutReplacingObservedEntities(bool riderObserved,
        bool mountObserved)
    {
        using var project = new CaptureProject();
        var interest = new SubsystemNetworkInterest();
        typeof(Subsystem).GetProperty(nameof(Subsystem.Project))!.SetValue(interest, project);
        typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(interest, true);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var mount = CreateBody(project, 10);
        var rider = CreateBody(project, 11);
        rider.ParentBody = mount;
        if (riderObserved)
        {
            interest.Entities.AddObserved(client, EntityInterestGroup.Creatures, 11);
        }

        if (mountObserved)
        {
            interest.Entities.AddObserved(client, EntityInterestGroup.Creatures, 10);
        }

        var pending = (List<OutboundPackage>)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(CommonLib.Net)!;
        var start = pending.Count;
        try
        {
            interest.EnsureBodyGroupObserved(client, rider);
            interest.EnsureBodyGroupObserved(client, mount);

            var expected = new List<Entity>();
            if (!mountObserved)
            {
                expected.Add(mount.Entity);
            }

            if (!riderObserved)
            {
                expected.Add(rider.Entity);
            }

            if (expected.Count == 0)
            {
                Assert.Equal(start, pending.Count);
            }
            else
            {
                var outbound = Assert.Single(pending.Skip(start));
                var baseline = Assert.IsType<EntityPackage>(outbound.Package);
                Assert.Equal(expected.Select(entity => entity.EntityId), project.CapturedEntityIds);
                Assert.NotEmpty(baseline.EntityData);
                Assert.True(outbound.Audience.Includes(client));
                var mountEvent = new ComponentMountPackage { Type = ComponentMountPackage.EventType.Mount };
                Assert.Equal(PackageTransportPolicy.Get(mountEvent).Channel,
                    PackageTransportPolicy.Get(baseline).Channel);
            }

            Assert.Same(client, Assert.Single(interest.Entities.GetObservers(EntityInterestGroup.Creatures, 10)));
            Assert.Same(client, Assert.Single(interest.Entities.GetObservers(EntityInterestGroup.Creatures, 11)));
        }
        finally
        {
            pending.RemoveRange(start, pending.Count - start);
        }
    }

    private static ComponentBody CreateBody(Project project, int id)
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        entity.EntityId = id;
        typeof(Entity).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(entity, project);
        var body = new ComponentBody();
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(body, entity);
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, new List<Component> { body });
        return body;
    }

    private sealed class CaptureProject : Project
    {
        public int[] CapturedEntityIds { get; private set; } = [];

        public override EntityDataList SaveEntitiesAll(IEnumerable<Entity> entities)
        {
            CapturedEntityIds = entities.Select(entity => entity.EntityId).ToArray();
            return new EntityDataList();
        }
    }

}
