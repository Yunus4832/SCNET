namespace Survivalcraft.Test.Modding;

public class ModFileServiceTest
{
    [Fact]
    public void GetModInfoDataEnumeratesScpakPackagesFromDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scnet-modfiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var previousDirectory = Game.Network.ModFileService.Utils.ModFileDirectory;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "alpha.scpak"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(root, "legacy.scmod"), [8, 8, 8]);
            File.WriteAllBytes(Path.Combine(root, "notes.txt"), [9, 9, 9]);
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllBytes(Path.Combine(root, "nested", "beta.scpak"), [4, 5, 6, 7]);

            Game.Network.ModFileService.Utils.ModFileDirectory = root;

            var infos = Game.Network.ModFileService.Utils.GetModInfoData();

            Assert.Equal(["alpha.scpak", "beta.scpak"], infos.Select(info => info.ModName));
            Assert.All(infos, info => Assert.NotEmpty(info.ModMd5));
        }
        finally
        {
            Game.Network.ModFileService.Utils.ModFileDirectory = previousDirectory;
            Directory.Delete(root, true);
        }
    }
}
