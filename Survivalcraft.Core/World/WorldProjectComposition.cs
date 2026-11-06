using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.World;

/// <summary>Selects world composition before resolving or constructing template classes.</summary>
public sealed class WorldProjectComposition(RuntimeSide side, bool hasPresentation) : IProjectComposition
{
    public bool ShouldCreateSubsystem(ValuesDictionary definition) => Includes(definition);

    public bool ShouldCreateComponent(ValuesDictionary definition) => Includes(definition);

    private bool Includes(ValuesDictionary definition)
    {
        return definition.GetValue("ConstructionSide", "Shared") switch
        {
            "Shared" => true,
            "Authority" => side == RuntimeSide.Authority,
            "Client" => side == RuntimeSide.Client,
            "Presentation" => side == RuntimeSide.Client && hasPresentation,
            var value => throw new InvalidOperationException($"Unknown world construction side \"{value}\".")
        };
    }
}
