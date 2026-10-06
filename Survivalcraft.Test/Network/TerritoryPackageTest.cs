using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class TerritoryPackageTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(15)]
    public void FlagsRoundTripAndOverwritePreviousValues(int flags)
    {
        var package = new TerritoriyPackage
        {
            Guid = Guid.NewGuid(),
            AllowDig = (flags & 1) != 0,
            AllowPlace = (flags & 2) != 0,
            ApplyToFriend = (flags & 4) != 0,
            IsVisible = (flags & 8) != 0
        };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new TerritoriyPackage
        {
            AllowDig = true,
            AllowPlace = true,
            ApplyToFriend = true,
            IsVisible = true
        };
        clone.ReadData(reader);

        Assert.Equal(package.Guid, clone.Guid);
        Assert.Equal(package.AllowDig, clone.AllowDig);
        Assert.Equal(package.AllowPlace, clone.AllowPlace);
        Assert.Equal(package.ApplyToFriend, clone.ApplyToFriend);
        Assert.Equal(package.IsVisible, clone.IsVisible);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
