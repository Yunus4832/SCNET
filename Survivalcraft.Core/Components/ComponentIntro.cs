using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.TerrainSerializers;

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

    public static Vector2 FindOceanDirection(ITerrainContentsGenerator generator, Vector2 position)
    {
        var num = float.MaxValue;
        var result = Vector2.Zero;
        for (var i = 0; i < 36; i++)
        {
            var vector = Vector2.CreateFromAngle(i / 36f * 2f * (float)Math.PI);
            var vector2 = position + 50f * vector;
            var num2 = generator.CalculateOceanShoreDistance(vector2.X, vector2.Y);
            if (!(num2 < num))
            {
                continue;
            }

            result = vector;
            num = num2;
        }

        return result;
    }
}
