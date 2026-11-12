using System.Globalization;

namespace Game.Blocks;

public class FireworksBlock : Block
{
    public enum Shape
    {
        SmallBurst = 0,

        LargeBurst = 1,

        Circle = 2,

        Disc = 3,

        Ball = 4,

        ShortTrails = 5,

        LongTrails = 6,

        FlatTrails = 7
    }

    public static readonly string[] HeadNames =
    [
        "HeadConeSmall",
        "HeadConeLarge",
        "HeadCylinderSmall",
        "HeadCylinderLarge",
        "HeadSphere",
        "HeadDiamondSmall",
        "HeadDiamondLarge",
        "HeadCylinderFlat"
    ];

    public static readonly Color[] FireworksColors =
    [
        new(255, 255, 255),
        new(85, 255, 255),
        new(255, 85, 85),
        new(85, 85, 255),
        new(255, 255, 85),
        new(85, 255, 85),
        new(255, 170, 0),
        new(255, 85, 255)
    ];


    public override IEnumerable<int> GetCreativeValues()
    {
        var color = 0;
        while (color < 8)
        {
            int num;
            for (var altitude = 0; altitude < 2; altitude = num)
            {
                for (var flickering = 0; flickering < 2; flickering = num)
                {
                    for (var shape = 0; shape < 8; shape = num)
                    {
                        yield return Terrain.MakeBlockValue(
                            BlockIndex,
                            SetColor(
                                SetAltitude(SetShape(SetFlickering(0, flickering != 0), (Shape)shape), altitude),
                                color
                            )
                        );
                        num = shape + 1;
                    }

                    num = flickering + 1;
                }

                num = altitude + 1;
            }

            num = color + 1;
            color = num;
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var shape = 0;
        while (shape < 8)
        {
            int num;
            for (var altitude = 0; altitude < 2; altitude = num)
            {
                for (var flickering = 0; flickering < 2; flickering = num)
                {
                    for (var color = 0; color < 8; color = num)
                    {
                        var craftingRecipe = new CraftingRecipe
                        {
                            ResultCount = 20,
                            ResultValue = Terrain.MakeBlockValue(
                                BlockIndex,
                                SetColor(
                                    SetAltitude(SetShape(SetFlickering(0, flickering != 0), (Shape)shape), altitude),
                                    color
                                )
                            ),
                            RemainsCount = 1,
                            RemainsValue = Terrain.MakeBlockValue(90),
                            RequiredHeatLevel = 0f,
                            Description = new LocalizedText(
                                nameof(FireworksBlock),
                                "CraftingDescription",
                                "Make fireworks"
                            )
                        };

                        switch (shape)
                        {
                            case 0:
                                craftingRecipe.Ingredients[0] = string.Empty;
                                craftingRecipe.Ingredients[1] = "sulphurchunk";
                                craftingRecipe.Ingredients[2] = string.Empty;
                                break;
                            case 1:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "coalchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 2:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = string.Empty;
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 3:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "sulphurchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 4:
                                craftingRecipe.Ingredients[0] = "coalchunk";
                                craftingRecipe.Ingredients[1] = "coalchunk";
                                craftingRecipe.Ingredients[2] = "coalchunk";
                                break;
                            case 5:
                                craftingRecipe.Ingredients[0] = string.Empty;
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = string.Empty;
                                break;
                            case 6:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 7:
                                craftingRecipe.Ingredients[0] = "coalchunk";
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = "coalchunk";
                                break;
                        }

                        switch (flickering)
                        {
                            case 0:
                                craftingRecipe.Ingredients[3] = "canvas";
                                craftingRecipe.Ingredients[5] = "canvas";
                                break;
                            case 1:
                                craftingRecipe.Ingredients[3] = "gunpowder";
                                craftingRecipe.Ingredients[5] = "gunpowder";
                                break;
                        }

                        switch (altitude)
                        {
                            case 0:
                                craftingRecipe.Ingredients[6] = "gunpowder";
                                craftingRecipe.Ingredients[7] = string.Empty;
                                craftingRecipe.Ingredients[8] = "gunpowder";
                                break;
                            case 1:
                                craftingRecipe.Ingredients[6] = "gunpowder";
                                craftingRecipe.Ingredients[7] = "gunpowder";
                                craftingRecipe.Ingredients[8] = "gunpowder";
                                break;
                        }

                        craftingRecipe.Ingredients[4] = "paintbucket:" +
                                                        (color != 7 ? color : 10)
                                                        .ToString(CultureInfo.InvariantCulture);
                        yield return craftingRecipe;
                        num = color + 1;
                    }

                    num = flickering + 1;
                }

                num = altitude + 1;
            }

            num = shape + 1;
            shape = num;
        }
    }

    public static Shape GetShape(int data)
    {
        return (Shape)(data & 7);
    }

    public static int SetShape(int data, Shape shape)
    {
        return (data & -8) | (int)(shape & Shape.FlatTrails);
    }

    public static int GetAltitude(int data)
    {
        return (data >> 3) & 1;
    }

    public static int SetAltitude(int data, int altitude)
    {
        return (data & -9) | ((altitude & 1) << 3);
    }

    public static bool GetFlickering(int data)
    {
        return ((data >> 4) & 1) != 0;
    }

    public static int SetFlickering(int data, bool flickering)
    {
        return (data & -17) | ((flickering ? 1 : 0) << 4);
    }

    public static int GetColor(int data)
    {
        return (data >> 5) & 7;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -225) | ((color & 7) << 5);
    }
}
