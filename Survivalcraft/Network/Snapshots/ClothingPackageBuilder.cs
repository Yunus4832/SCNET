namespace Game.Network.Snapshots;

public static class ClothingPackageBuilder
{
    public static ComponentClothingPackage Capture(string skinName, ComponentClothingPackage.DataType type)
    {
        var data = Array.Empty<byte>();
        if (type is ComponentClothingPackage.DataType.ReplySkin or ComponentClothingPackage.DataType.WhoHasReply && CharacterSkinsManager.HasSkinRes(skinName))
        {
            using var stream = Storage.OpenFile(Storage.CombinePaths(GamePaths.CharacterSkins, skinName), OpenFileMode.Read);
            data = StreamUtils.ReadBytes(stream);
        }
        return new ComponentClothingPackage(skinName, type) { SkinData = data };
    }
}
