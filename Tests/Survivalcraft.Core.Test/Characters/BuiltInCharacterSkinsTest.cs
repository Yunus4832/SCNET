using Game.Characters;

namespace Survivalcraft.Core.Test.Characters;

public sealed class BuiltInCharacterSkinsTest
{
    [Fact]
    public void BuiltInIdentityDefaultsAreAvailableWithoutAnAssetStore()
    {
        Assert.Equal(8, BuiltInCharacterSkins.Definitions.Count);
        Assert.Equal(4, BuiltInCharacterSkins.Definitions.Count(definition => definition.Sex == CharacterSex.Male));
        Assert.Equal(4, BuiltInCharacterSkins.Definitions.Count(definition => definition.Sex == CharacterSex.Female));
        Assert.Equal(8, BuiltInCharacterSkins.Definitions.Select(definition => definition.Name).Distinct().Count());
        foreach (var definition in BuiltInCharacterSkins.Definitions)
        {
            Assert.True(BuiltInCharacterSkins.IsBuiltIn(definition.Name));
            Assert.Equal(definition.DefaultCharacterName, BuiltInCharacterSkins.GetDefaultName(definition.Name));
        }

        Assert.Equal("$Male1", BuiltInCharacterSkins.Definitions[0].Name);
        Assert.Equal("Walter", BuiltInCharacterSkins.GetDefaultName("$Male1"));
        Assert.Equal("Shirley", BuiltInCharacterSkins.GetDefaultName("$Female4"));
        Assert.False(BuiltInCharacterSkins.IsBuiltIn("custom-skin"));
        Assert.Throws<ArgumentException>(() => BuiltInCharacterSkins.GetDefaultName("custom-skin"));
    }
}
