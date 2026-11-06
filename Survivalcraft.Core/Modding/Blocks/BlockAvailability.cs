namespace Game.Modding.Blocks;

public static class BlockAvailability
{
    public static bool IsBlockDisabled(BlockContentCatalog catalog, string disabledBlocks, int value)
    {
        var arr = disabledBlocks.Split(new[] { ';' });
        foreach (var item in arr)
        {
            if (string.IsNullOrEmpty(item))
            {
                continue;
            }

            CraftingRecipeCatalog.DecodeIngredient(item, out var craftId, out var data);
            var blocks = catalog.FindBlocksByCraftingId(craftId);
            foreach (var b in blocks)
            {
                var vv = Terrain.MakeBlockValue(b.BlockIndex, 0, data ?? 0);
                if (value == vv)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
