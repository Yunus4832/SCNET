using Engine.Core.Mathematics;

using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class PlayerDropValidationTest
{
    [Theory]
    [InlineData(-1, 1, 1, false)]
    [InlineData(10, 1, 1, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, -1, 1, false)]
    [InlineData(0, 2, 1, false)]
    [InlineData(0, 1, 1, true)]
    public void SlotAndCountMustMatchAvailableInventory(int slot, int count, int available, bool valid)
    {
        Assert.Equal(
            valid,
            ComponentPlayerPackageHandler.AreDropParametersValid(
                slot,
                10,
                count,
                available,
                new Vector3(12f, 0f, 0f)
            )
        );
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(13f)]
    public void DropRejectsNonFiniteOrExcessiveVelocity(float velocity)
    {
        Assert.False(
            ComponentPlayerPackageHandler.AreDropParametersValid(
                0,
                10,
                1,
                1,
                new Vector3(velocity, 0f, 0f)
            )
        );
    }
}
