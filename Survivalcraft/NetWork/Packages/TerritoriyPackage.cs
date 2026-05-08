namespace Game.NetWork.Packages;

public class TerritoriyPackage : IPackage
{
    private bool _allowBehavior;
    private bool _allowDig;
    private bool _allowPlace;
    private bool _applyToFirend;

    private Guid _guid;
    private bool _isVisible;

    public TerritoriyPackage()
    {
    }

    public TerritoriyPackage(Territoriy territoriy)
    {
        _guid = territoriy.OwnerGuid;
        _allowBehavior = territoriy.AllowBlockBehavior;
        _allowDig = territoriy.AllowDig;
        _allowPlace = territoriy.AllowPlace;
        _applyToFirend = territoriy.ApplyToFriend;
        _isVisible = territoriy.IsVisible;
    }

    public byte ID => (byte)PackageType.Territoriy;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_guid);
        byte flag = 0;
        if (_allowBehavior)
        {
            flag |= 1;
        }

        if (_allowDig)
        {
            flag |= 2;
        }

        if (_allowPlace)
        {
            flag |= 4;
        }

        if (_applyToFirend)
        {
            flag |= 8;
        }

        if (_isVisible)
        {
            flag |= 16;
        }

        writer.Write(flag);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _guid = reader.ReadGuid();
        var flag = reader.ReadByte();
        if ((flag & 1) == 1)
        {
            _allowBehavior = true;
        }

        if (((flag >> 1) & 1) == 1)
        {
            _allowDig = true;
        }

        if (((flag >> 2) & 1) == 1)
        {
            _allowPlace = true;
        }

        if (((flag >> 3) & 1) == 1)
        {
            _applyToFirend = true;
        }

        if (((flag >> 4) & 1) == 1)
        {
            _isVisible = true;
        }
    }

    public void Handle(ProjectNet? projectNet, NetNode netNode, bool isServer)
    {
        if (SubsystemBedrockBlockBehavior.Territories.TryGetValue(_guid, out var territoriy))
        {
            //territoriy.AllowBlockBehavior = _allowBehavior;
            territoriy.AllowDig = _allowDig;
            territoriy.AllowPlace = _allowPlace;
            territoriy.ApplyToFriend = _applyToFirend;
            territoriy.IsVisible = _isVisible;
            if (isServer)
            {
                Except = From;
                netNode.QueuePackage(this);
            }
        }
    }
}
