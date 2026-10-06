using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class FurnitureSetNamingTest
{
    [Fact]
    public void DuplicateNamesUseStableSuffixInsteadOfAccumulatingSuffixes()
    {
        var behavior = new SubsystemFurnitureBlockBehavior();

        Assert.Equal("set", behavior.NewFurnitureSet("set", "").Name);
        Assert.Equal("set1", behavior.NewFurnitureSet("set", "").Name);
        Assert.Equal("set2", behavior.NewFurnitureSet("set", "").Name);
    }

    [Fact]
    public void RenameAvoidsAnotherSetButDoesNotConflictWithItself()
    {
        var behavior = new SubsystemFurnitureBlockBehavior();
        var original = behavior.NewFurnitureSet("original", "");
        behavior.NewFurnitureSet("target", "");

        behavior.RenameFurnitureSet(original, "target");
        Assert.Equal("target1", original.Name);
        behavior.RenameFurnitureSet(original, "target1");
        Assert.Equal("target1", original.Name);
    }

    [Fact]
    public void CollisionSuffixStaysWithinNameLimit()
    {
        var behavior = new SubsystemFurnitureBlockBehavior();
        var name = new string('a', SubsystemFurnitureBlockBehavior.MaxFurnitureSetNameLength);
        behavior.NewFurnitureSet(name, "");

        var duplicate = behavior.NewFurnitureSet(name, "");

        Assert.Equal(SubsystemFurnitureBlockBehavior.MaxFurnitureSetNameLength, duplicate.Name.Length);
        Assert.EndsWith("1", duplicate.Name);
    }
}
