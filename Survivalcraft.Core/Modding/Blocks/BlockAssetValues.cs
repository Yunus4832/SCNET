using System.Collections.ObjectModel;

namespace Game.Modding.Blocks;

public sealed record BlockAssetValue(string Column, string Value);

public static class BlockAssetValues
{
    public static IReadOnlyDictionary<string, Type> Columns { get; } = new ReadOnlyDictionary<string, Type>(
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Category"] = typeof(string),
            ["Description"] = typeof(string),
            ["DisplayName"] = typeof(string),
            ["IconBlockOffset"] = typeof(Vector3),
            ["IconViewOffset"] = typeof(Vector3),
            ["IconViewScale"] = typeof(float),
            ["ShadowStrength"] = typeof(int),
            ["TextureSlot"] = typeof(int),
            ["DestructionDebrisScale"] = typeof(float),
            ["DisplayOrder"] = typeof(int),
            ["FirstPersonOffset"] = typeof(Vector3),
            ["FirstPersonRotation"] = typeof(Vector3),
            ["FirstPersonScale"] = typeof(float),
            ["GenerateFacesForSameNeighbors"] = typeof(bool),
            ["InHandOffset"] = typeof(Vector3),
            ["InHandRotation"] = typeof(Vector3),
            ["InHandScale"] = typeof(float),
            ["ObjectShadowStrength"] = typeof(float),
        }
    );
}
