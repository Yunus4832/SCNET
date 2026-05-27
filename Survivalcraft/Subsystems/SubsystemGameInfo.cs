using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemGameInfo : Subsystem, IUpdateable
{
    private double? _lastTotalElapsedGameTime;

    private SubsystemTime _subsystemTime = null!;

    public SubsystemTimeOfDay TimeOfDay = null!;

    public WorldSettings WorldSettings { get; set; } = null!;

    public string DirectoryName { get; set; } = string.Empty;

    public double TotalElapsedGameTime { get; set; }

    public float TotalElapsedGameTimeDelta { get; set; }

    public int WorldSeed { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        TotalElapsedGameTime += dt;
        TotalElapsedGameTimeDelta = _lastTotalElapsedGameTime.HasValue
            ? (float)(TotalElapsedGameTime - _lastTotalElapsedGameTime.Value)
            : 0f;
        _lastTotalElapsedGameTime = TotalElapsedGameTime;
        if (WorldSettings.AreSeasonsChanging && _subsystemTime.PeriodicGameTimeEvent(10.0, 5.0))
        {
            var num = WorldSettings.YearDays * 1200f;
            WorldSettings.TimeOfYear = IntervalUtils.Normalize(WorldSettings.TimeOfYear + 10f / num);
        }

        if (_subsystemTime.GameTime >= 600.0 && _subsystemTime.GameTime - _subsystemTime.GameTimeDelta < 600.0 &&
            UserManager.ActiveUser != null)
        {
            foreach (var item in GetActiveExternalContent())
            {
                CommunityContentManager.SendPlayTime(
                    item.Address,
                    UserManager.ActiveUser.UniqueId,
                    _subsystemTime.GameTime,
                    new CancellableProgress(),
                    Actions.Empty,
                    delegate { }
                );
            }
        }

        //客户端禁用时间计算
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            CommonLib.Net.QueuePackage(new SubsystemTimePackage(TotalElapsedGameTime, TimeOfDay.TimeOfDayOffset));
        }
    }

    public IEnumerable<ActiveExternalContentInfo> GetActiveExternalContent()
    {
        var downloadedContentAddress =
            CommunityContentManager.GetDownloadedContentAddress(ExternalContentType.World, DirectoryName);
        if (!string.IsNullOrEmpty(downloadedContentAddress))
        {
            yield return new ActiveExternalContentInfo
            {
                Address = downloadedContentAddress,
                DisplayName = WorldSettings.Name,
                Type = ExternalContentType.World
            };
        }

        if (!BlocksTexturesManager.IsBuiltIn(WorldSettings.BlocksTextureName))
        {
            downloadedContentAddress =
                CommunityContentManager.GetDownloadedContentAddress(ExternalContentType.BlocksTexture,
                    WorldSettings.BlocksTextureName);
            if (!string.IsNullOrEmpty(downloadedContentAddress))
            {
                yield return new ActiveExternalContentInfo
                {
                    Address = downloadedContentAddress,
                    DisplayName = BlocksTexturesManager.GetDisplayName(WorldSettings.BlocksTextureName),
                    Type = ExternalContentType.BlocksTexture
                };
            }
        }

        var subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        foreach (var playersDatum in subsystemPlayers.PlayersData)
        {
            if (!CharacterSkinsManager.IsBuiltIn(playersDatum.CharacterSkinName))
            {
                downloadedContentAddress =
                    CommunityContentManager.GetDownloadedContentAddress(ExternalContentType.CharacterSkin,
                        playersDatum.CharacterSkinName);
                yield return new ActiveExternalContentInfo
                {
                    Address = downloadedContentAddress,
                    DisplayName = CharacterSkinsManager.GetDisplayName(playersDatum.CharacterSkinName),
                    Type = ExternalContentType.CharacterSkin
                };
            }
        }

        var subsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        foreach (var furnitureSet in subsystemFurnitureBlockBehavior.FurnitureSets)
        {
            if (!string.IsNullOrEmpty(furnitureSet.ImportedFrom))
            {
                downloadedContentAddress =
                    CommunityContentManager.GetDownloadedContentAddress(ExternalContentType.FurniturePack,
                        furnitureSet.ImportedFrom);
                if (string.IsNullOrEmpty(downloadedContentAddress))
                {
                    continue;
                }

                {
                    yield return new ActiveExternalContentInfo
                    {
                        Address = downloadedContentAddress,
                        DisplayName = FurniturePacksManager.GetDisplayName(furnitureSet.ImportedFrom),
                        Type = ExternalContentType.FurniturePack
                    };
                }
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        TimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        WorldSettings = new WorldSettings();
        WorldSettings.Load(valuesDictionary);
        DirectoryName = valuesDictionary.GetValue<string>("WorldDirectoryName");
        TotalElapsedGameTime = valuesDictionary.GetValue<double>("TotalElapsedGameTime");
        WorldSeed = valuesDictionary.GetValue<int>("WorldSeed");
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        WorldSettings.Save(valuesDictionary, false);
        valuesDictionary.SetValue("WorldSeed", WorldSeed);
        valuesDictionary.SetValue("TotalElapsedGameTime", TotalElapsedGameTime);
    }
}
