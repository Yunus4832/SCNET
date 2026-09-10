using System.Globalization;
using System.Security.Cryptography;

namespace Game;

public static class WorldSeedConverter
{
    private const string _randomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string CreateRandomTextSeed()
    {
        return string.Create(8, 0, static (characters, _) =>
        {
            for (var i = 0; i < characters.Length; i++)
            {
                characters[i] = _randomCharacters[RandomNumberGenerator.GetInt32(_randomCharacters.Length)];
            }
        });
    }

    public static int FromText(string seed)
    {
        if (int.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericSeed))
        {
            return numericSeed;
        }

        var worldSeed = 0;
        var multiplier = 1;
        unchecked
        {
            foreach (var character in seed)
            {
                worldSeed += character * multiplier;
                multiplier += 29;
            }
        }

        return worldSeed;
    }
}
