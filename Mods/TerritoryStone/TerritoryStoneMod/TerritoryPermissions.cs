namespace TerritoryStoneMod;

public static class TerritoryPermissions
{
    public static bool IsValid(TerritoryPermission permissions) => (permissions & ~TerritoryPermission.All) == 0;

    public static bool TryParse(string text, out TerritoryPermission permissions)
    {
        permissions = TerritoryPermission.None;
        if (text == "none")
        {
            return true;
        }

        if (text == "all")
        {
            permissions = TerritoryPermission.All;
            return true;
        }

        foreach (var token in text.Split(','))
        {
            var value = token switch
            {
                "enter" => TerritoryPermission.Enter,
                "exit" => TerritoryPermission.Exit,
                "dig" => TerritoryPermission.Dig,
                "place" => TerritoryPermission.Place,
                _ => TerritoryPermission.None
            };
            if (value == TerritoryPermission.None)
            {
                permissions = TerritoryPermission.None;
                return false;
            }

            permissions |= value;
        }

        return permissions != TerritoryPermission.None;
    }
}
