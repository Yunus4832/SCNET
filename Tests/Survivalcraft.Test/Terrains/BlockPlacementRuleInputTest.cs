using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Terrains;

public sealed class BlockPlacementRuleInputTest
{
    [Theory]
    [InlineData(0f, 1f, 2)]
    [InlineData(1f, 0f, 3)]
    [InlineData(0f, -1f, 0)]
    [InlineData(-1f, 0f, 1)]
    public void StairsPlacementUsesTheSuppliedEyeDirection(float x, float z, int rotation)
    {
        using var block = new CobblestoneStairsBlock { BlockIndex = 48 };
        var result = block.GetPlacementValue(
            null!,
            new Vector3(x, 0f, z),
            0,
            new TerrainRaycastResult { CellFace = new CellFace(1, 2, 3, 5) }
        );
        var data = Terrain.ExtractData(result.Value);
        Assert.Equal(rotation, StairsBlock.GetRotation(data));
        Assert.True(StairsBlock.GetIsUpsideDown(data));
        Assert.Equal(BlockPlacementRejection.None, result.Rejection);
    }

    [Fact]
    public void MagnetLimitReturnsARejectionWithoutAPlayerOrPresentation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var magnets = new SubsystemMagnetBlockBehavior();
        project.AddSubsystem(magnets);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        for (var i = 0; i < SubsystemMagnetBlockBehavior.MaxMagnets; i++)
        {
            magnets.OnBlockAdded(0, 0, i, 1, 1);
        }

        using var block = new MagnetBlock();
        var result = block.GetPlacementValue(terrain, Vector3.UnitZ, 0, default);
        Assert.Equal(0, result.Value);
        Assert.Equal(BlockPlacementRejection.MagnetLimit, result.Rejection);
    }
}
