using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class DeciduousLeavesBlockPresentation(
    DeciduousLeavesBlock block,
    BlockPresentationCatalog catalog,
    BlockColorsMap blockColorsMap,
    Color autumnColor1,
    Color autumnColor2,
    float autumnTransitionLightening
) : LeavesBlockPresentation(block, catalog)
{
    protected new DeciduousLeavesBlock Block => (DeciduousLeavesBlock)base.Block;

    public override bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        if (!base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue, x, y, z))
        {
            return false;
        }

        if (Terrain.ExtractContents(value) != Terrain.ExtractContents(neighborValue))
        {
            return true;
        }

        var data = Terrain.ExtractData(value);
        var data2 = Terrain.ExtractData(neighborValue);
        var num = DeciduousLeavesBlock.GetSeason(data) == Season.Winter;
        var flag = DeciduousLeavesBlock.GetSeason(data2) == Season.Winter;
        return num != flag;
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var displayName = base.GetDisplayName(subsystemTerrain, value);
        var season = DeciduousLeavesBlock.GetSeason(Terrain.ExtractData(value));
        var seasonPrefix = season switch
        {
            Season.Summer => Localization.ClientLanguage.Catalog.Get("DeciduousLeavesBlock", 0),
            Season.Autumn => Localization.ClientLanguage.Catalog.Get("DeciduousLeavesBlock", 1),
            Season.Winter => Localization.ClientLanguage.Catalog.Get("DeciduousLeavesBlock", 2),
            _ => Localization.ClientLanguage.Catalog.Get("DeciduousLeavesBlock", 3)
        };

        return Localization.ClientLanguage.Resolver.Get(
            "BlockNames",
            "Seasonal",
            "{0} {1}",
            seasonPrefix.Trim(),
            displayName
        );
    }

    public override int GetShadowStrength(int value)
    {
        var season = DeciduousLeavesBlock.GetSeason(Terrain.ExtractData(value));

        var shadowStrength = season switch
        {
            Season.Winter => base.GetShadowStrength(value) / 3,
            Season.Spring => base.GetShadowStrength(value) / 2,
            _ => base.GetShadowStrength(value)
        };

        return shadowStrength;
    }

    public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
    {
        var data = Terrain.ExtractData(value);
        switch (DeciduousLeavesBlock.GetSeason(data))
        {
            case Season.Spring:
            {
                var c3 = BlockColorsMap.Lookup(terrain, x, y, z);
                var timeOfSeason = DeciduousLeavesBlock.GetTimeOfSeason(data);
                return Color.LerpNotSaturated(SpringColor, c3, timeOfSeason);
            }
            case Season.Autumn:
            {
                var c = BlockColorsMap.Lookup(terrain, x, y, z);
                var c2 = Color.LerpNotSaturated(
                    f: MathUtils.Hash((uint)(x + 59 * y + 2497 * z)) / 4.2949673E+09f,
                    c1: AutumnColor1,
                    c2: AutumnColor2
                );
                var f2 = MathUtils.Min(DeciduousLeavesBlock.GetTimeOfSeason(data) * Block.AutumnSpeedupFactor, 1f);
                return Color.MultiplyColorOnly(
                    s: MathUtils.Lerp(1f, AutumnTransitionLightening, DeciduousLeavesBlock.Hat(f2)),
                    c: Color.LerpNotSaturated(c, c2, f2)
                );
            }
            case Season.Winter:
                return Color.White;
            case Season.Summer:
            default:
                return BlockColorsMap.Lookup(terrain, x, y, z);
        }
    }

    public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
    {
        var data = Terrain.ExtractData(value);
        switch (DeciduousLeavesBlock.GetSeason(data))
        {
            case Season.Spring:
            {
                var c3 = BlockColorsMap.Lookup(environmentData);
                var timeOfSeason = DeciduousLeavesBlock.GetTimeOfSeason(data);
                return Color.LerpNotSaturated(SpringColor, c3, timeOfSeason);
            }
            case Season.Autumn:
            {
                var c = BlockColorsMap.Lookup(environmentData);
                var c2 = Color.Lerp(AutumnColor1, AutumnColor2, 0.5f);
                var f = MathUtils.Min(DeciduousLeavesBlock.GetTimeOfSeason(data) * Block.AutumnSpeedupFactor, 1f);
                return Color.MultiplyColorOnly(
                    s: MathUtils.Lerp(1f, AutumnTransitionLightening, DeciduousLeavesBlock.Hat(f)),
                    c: Color.LerpNotSaturated(c, c2, f)
                );
            }
            case Season.Winter:
                return Color.White;
            case Season.Summer:
            default:
                return BlockColorsMap.Lookup(environmentData);
        }
    }

    public readonly BlockColorsMap BlockColorsMap = blockColorsMap;

    public readonly Color AutumnColor1 = autumnColor1;

    public readonly Color AutumnColor2 = autumnColor2;

    public readonly float AutumnTransitionLightening = autumnTransitionLightening;

    public readonly Color SpringColor = new(160, 255, 90);

    public override void OnDug(SubsystemTerrain terrain, SubsystemParticles particles, BlockDigOutcome outcome)
    {
        if (DestructionDebrisScale <= 0f) { return; }

        if (outcome.Kind == BlockDigOutcomeKind.LeavesShaken)
        {
            particles.AddParticleSystem(
                new LeavesParticleSystem(terrain, outcome.CellFace.Point, 8, false, outcome.OldValue)
            );
            return;
        }

        base.OnDug(terrain, particles, outcome);
    }
}
