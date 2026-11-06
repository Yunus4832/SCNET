namespace Game.Characters;

/// <summary>Built-in character identity defaults, independent of asset storage and presentation.</summary>
public static class BuiltInCharacterSkins
{
    public sealed record Definition(string Name, CharacterSex Sex, string DefaultCharacterName);

    public static IReadOnlyList<Definition> Definitions { get; } = Array.AsReadOnly<Definition>(
        [
            new("$Male1", CharacterSex.Male, "Walter"),
            new("$Male2", CharacterSex.Male, "Basil"),
            new("$Male3", CharacterSex.Male, "Geoffrey"),
            new("$Male4", CharacterSex.Male, "Zachary"),
            new("$Female1", CharacterSex.Female, "Doris"),
            new("$Female2", CharacterSex.Female, "Mabel"),
            new("$Female3", CharacterSex.Female, "Ada"),
            new("$Female4", CharacterSex.Female, "Shirley")
        ]
    );

    public static bool IsBuiltIn(string name) => name.StartsWith('$');

    public static string GetDefaultName(string name)
    {
        if (!IsBuiltIn(name)) { throw new ArgumentException("A built-in character skin is required.", nameof(name)); }

        var sex = name.Contains("Female") ? CharacterSex.Female : CharacterSex.Male;
        var variant = name.Contains('1') ? 0 : name.Contains('2') ? 1 : name.Contains('3') ? 2 : 3;
        return Definitions.Where(definition => definition.Sex == sex).ElementAt(variant).DefaultCharacterName;
    }
}
