using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentSicknessPackage : IPackage
{
    public enum EventType
    {
        SyncStat,
        NauseaEffect
    }

    private int _entityId;

    private float _sicknessDuration;

    public byte ID => (byte)PackageType.ComponentSickness;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentSicknessPackage()
    {
    }

    public ComponentSicknessPackage(ComponentSickness sickness)
    {
        _entityId = sickness.Entity.EntityId;
        _sicknessDuration = sickness.SicknessDuration;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_entityId);
        writer.Write(_sicknessDuration);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _entityId = reader.ReadInt32();
        _sicknessDuration = reader.ReadSingle();
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        project.FindEntityById(_entityId, entity =>
        {
            var sickness = entity.FindComponent<ComponentSickness>();
            if (sickness == null)
            {
                return;
            }

            sickness.SicknessDuration = _sicknessDuration;
            if (_sicknessDuration > 0f)
            {
                sickness.NauseaEffect();
            }
        });
    }
}
