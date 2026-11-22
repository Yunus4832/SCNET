using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.World;

/// <summary>解析或构造模板类之前确定世界的组成。</summary>
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
