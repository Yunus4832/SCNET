using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemEggBlockBehavior : SubsystemBlockBehavior
{
    private EggBlock _eggBlock = null!;

    private readonly Random _random = new();

    private SubsystemCreatureSpawn _subsystemCreatureSpawn = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public override ResourceId[] HandledBlocks => [];

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        var data = Terrain.ExtractData(worldItem.Value);
        var isCooked = EggBlock.GetIsCooked(data);
        var isLaid = EggBlock.GetIsLaid(data);
        if (isCooked ||
            (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
             !(_random.Float(0f, 1f) <= (isLaid ? 0.15f : 1f))))
        {
            return true;
        }

        const int maxCount = 35 * 1000;
        if (_subsystemCreatureSpawn.Creatures.Count < maxCount)
        {
            var eggType = _eggBlock.GetEggType(data);
            _subsystemCreatureSpawn.CreateEntity(eggType.TemplateName, worldItem.Position, null, null);
        }
        else
        {
            ReportCapacityLimit((worldItem as Projectile)?.Owner as ComponentCharacter);
        }

        return true;
    }

    internal void ReportCapacityLimit(ComponentCharacter? actor)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            actor is null ||
            !ReferenceEquals(actor.Project, Project) ||
            actor.CharacterData.ControllerConnection is not { } recipient)
        {
            return;
        }

        Project.FindSubsystem<SubsystemMessages>(true)!.Service.Publish(
            GameMessageLocalization.System(
                    "SubsystemEggBlockBehavior",
                    "CapacityLimit",
                    presentation: GameMessagePresentation.Toast
                ) with
                {
                    ToastStyle = new GameMessageToastStyle(GameMessageTone.Normal, true, false)
                },
            [recipient.ID],
            includePublisher: false
        );
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _eggBlock = (EggBlock)SubsystemTerrain.Catalog.GetBlock(ResourceId.Parse("game:egg"));
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
    }
}
