using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Celebrations;

namespace Game.World;

/// <summary>Constructs an authority project using explicitly prepared content and host policies.</summary>
public static class AuthorityWorldProjectFactory
{
    public static Project Create(
        WorldRuntimeContext runtime,
        BlockContentCatalog catalog,
        GameDatabase database,
        XElement projectNode,
        ValuesDictionary overrides,
        AuthorityWorldProjectPolicies policies,
        Action<XElement> persistMetadata
    )
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        if (!runtime.IsAuthority || runtime.HasPresentation || runtime.HasProjectResources)
        {
            throw new InvalidOperationException("Authority construction requires an unused authority world.");
        }

        try
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(projectNode);
            ArgumentNullException.ThrowIfNull(overrides);
            ArgumentNullException.ThrowIfNull(policies);
            ArgumentNullException.ThrowIfNull(policies.Spawn);
            ArgumentNullException.ThrowIfNull(policies.Interest);
            ArgumentNullException.ThrowIfNull(policies.Celebration);
            ArgumentNullException.ThrowIfNull(policies.ShouldCollectAfterRosterChange);
            ArgumentNullException.ThrowIfNull(persistMetadata);
            if (!ReferenceEquals(WorldContentBinding.For(runtime).Catalog, catalog))
            {
                throw new InvalidOperationException(
                    "Authority content must be bound to this world before project loading."
                );
            }

            var previousMapping = projectNode.Element("BlockRegistry");
            previousMapping?.Remove();
            projectNode.Add(catalog.Registry.Mapping.ToXml());
            if (previousMapping is not null || catalog.Registry.Mapping.Entries.Count > 0)
            {
                persistMetadata(projectNode);
            }

            var data = new ProjectData(database, projectNode, overrides, true);
            return new Project(
                database,
                data,
                runtime.BindProject,
                new WorldProjectComposition(runtime.Side, runtime.HasPresentation),
                subsystem => Configure(runtime, subsystem, policies)
            );
        }
        catch
        {
            runtime.Close();
            throw;
        }
    }

    private static void Configure(
        WorldRuntimeContext runtime,
        Subsystem subsystem,
        AuthorityWorldProjectPolicies policies
    )
    {
        if (subsystem is SubsystemCreatureSpawn spawn) { spawn.ConfigurePolicy(policies.Spawn); }

        if (subsystem is SubsystemNetworkInterest interest) { interest.ConfigurePolicy(policies.Interest); }

        if (subsystem is SubsystemNewYearCelebrationHosting celebration)
        {
            celebration.Configure(policies.Celebration);
        }

        if (subsystem is SubsystemCharacters characters)
        {
            runtime.OwnWorldResource(
                new CharacterRosterMemoryPolicy(
                    runtime,
                    characters,
                    policies.ShouldCollectAfterRosterChange,
                    GC.Collect
                )
            );
        }
    }
}
