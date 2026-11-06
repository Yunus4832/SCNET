namespace Game.Application;

public sealed record ClientStoragePaths(
    string External,
    string ScreenCaptures,
    string CharacterSkins,
    string FurniturePacks,
    string BlockTextures,
    string ContentPackageCache,
    string ContentPackageCreationTemp,
    string Logs
);
