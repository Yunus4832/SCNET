using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Terrains.Generation;

namespace Game.Components;

public class ComponentIntro : Component
{
    public bool PlayIntro { get; private set; }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        PlayIntro = valuesDictionary.GetValue<bool>("PlayIntro");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("PlayIntro", PlayIntro);
    }

    public void AcknowledgePresentation()
    {
        if (WorldRuntimeContext.IsProjectActive(Project) && WorldRuntimeContext.For(Project).IsAuthority)
        {
            PlayIntro = false;
        }
    }

    public static Vector2 FindOceanDirection(ITerrainEnvironment environment, Vector2 position)
    {
        var closestShoreDistance = float.MaxValue;
        var result = Vector2.Zero;
        for (var i = 0; i < 36; i++)
        {
            var vector = Vector2.CreateFromAngle(i / 36f * 2f * (float)Math.PI);
            var samplePosition = position + 50f * vector;
            var shoreDistance = environment.CalculateOceanShoreDistance(samplePosition.X, samplePosition.Y);
            if (!(shoreDistance < closestShoreDistance))
            {
                continue;
            }

            result = vector;
            closestShoreDistance = shoreDistance;
        }

        return result;
    }
}
