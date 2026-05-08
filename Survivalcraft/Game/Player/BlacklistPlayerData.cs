namespace Game;

public class BlacklistPlayerData(Guid playerGUID, string name)
{
    public Guid PlayerGUID { get; set; } = playerGUID;

    public string Name
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            IsDefaultName = false;
        }
    } = name;

    public bool IsDefaultName { get; set; }
}
