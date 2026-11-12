using System.Globalization;

namespace Game.Blocks;

public class PumpkinSoupBucketBlock : BucketBlock
{
    public override int GetDamageDestructionValue(int value)
    {
        return 252;
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var isDead = 0;
        while (isDead <= 1)
        {
            int num;
            for (var rot = 0; rot <= 1; rot = num)
            {
                var craftingRecipe = new CraftingRecipe
                {
                    ResultCount = 1,
                    ResultValue = BlockIndex,
                    RequiredHeatLevel = 1f,
                    Description = new LocalizedText(
                        nameof(PumpkinSoupBucketBlock),
                        "CraftingDescription",
                        "Cook pumpkin soup"
                    )
                };
                var data = BasePumpkinBlock.SetIsDead(BasePumpkinBlock.SetSize(0, 7), isDead != 0);
                var value = SetDamage(Terrain.MakeBlockValue(131, data), rot);
                craftingRecipe.Ingredients[0] =
                    "pumpkin:" + Terrain.ExtractData(value).ToString(CultureInfo.InvariantCulture);
                craftingRecipe.Ingredients[1] = "waterbucket";
                yield return craftingRecipe;
                num = rot + 1;
            }

            num = isDead + 1;
            isDead = num;
        }
    }
}
