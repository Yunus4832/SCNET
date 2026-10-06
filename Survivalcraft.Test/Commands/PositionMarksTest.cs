using Engine.Core;

using Game;

namespace Survivalcraft.Test.Commands;

public class PositionMarksTest
{
    [Fact]
    public void ScopesRemainIndependentAndNamesOverwriteCaseInsensitively()
    {
        var personal = new PositionMarks();
        var otherPlayer = new PositionMarks();
        var shared = new PositionMarks();
        personal.Set("Home", new Vector3(1f, 65f, 1f));
        personal.Set("HOME", new Vector3(2f, 65f, 2f));
        shared.Set("home", new Vector3(3f, 65f, 3f));
        Assert.True(personal.TryGet("home", out var personalPoint));
        Assert.Equal(new Vector3(2f, 65f, 2f), personalPoint);
        Assert.True(shared.TryGet("HOME", out var publicPoint));
        Assert.Equal(new Vector3(3f, 65f, 3f), publicPoint);
        Assert.False(otherPlayer.TryGet("home", out _));
        Assert.Single(personal.Save());
    }

    [Fact]
    public void MarksRoundTripAndLoadingReplacesPreviousWorldData()
    {
        var original = new PositionMarks();
        original.Set("出生点", new Vector3(-20f, 65f, 30f));
        var restored = new PositionMarks();
        restored.Set("stale", Vector3.Zero);
        restored.Load(original.Save());
        Assert.False(restored.TryGet("stale", out _));
        Assert.True(restored.TryGet("出生点", out var position));
        Assert.Equal(new Vector3(-20f, 65f, 30f), position);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("home ", false)]
    [InlineData("a\nb", false)]
    [InlineData("My home", true)]
    [InlineData("家", true)]
    public void NamesAreBoundedAndUsable(string name, bool valid)
    {
        Assert.Equal(valid, PositionMarks.IsValidName(name));
        Assert.True(PositionMarks.IsValidName(new string('a', 64)));
        Assert.False(PositionMarks.IsValidName(new string('a', 65)));
    }
}
