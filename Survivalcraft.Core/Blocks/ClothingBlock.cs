using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.Blocks;

public class ClothingBlock : Block
{
    private DynamicArray<ClothingData?> _clothingData = [];


    private XElement? _initializationData;


    public int Num;

    public void LoadClothingData(XElement item)
    {
        if (item.Name.LocalName == "ClothingData")
        {
            int.TryParse(item.Attribute("Index")?.Value, out var clothIndex);
            var newDescription = item.Attribute("Description")?.Value;
            var newDisplayName = item.Attribute("DisplayName")?.Value;
            var clothingData = new ClothingData
            {
                Index = clothIndex,
                DisplayIndex = Num,
                DisplayName = newDisplayName ?? string.Empty,
                Slot = XmlUtils.GetAttributeValue<ClothingSlot>(item, "Slot"),
                ArmorProtection = XmlUtils.GetAttributeValue<float>(item, "ArmorProtection"),
                Sturdiness = XmlUtils.GetAttributeValue<float>(item, "Sturdiness"),
                Insulation = XmlUtils.GetAttributeValue<float>(item, "Insulation"),
                MovementSpeedFactor = XmlUtils.GetAttributeValue<float>(item, "MovementSpeedFactor"),
                SteedMovementSpeedFactor = XmlUtils.GetAttributeValue<float>(item, "SteedMovementSpeedFactor"),
                DensityModifier = XmlUtils.GetAttributeValue<float>(item, "DensityModifier"),
                IsOuter = XmlUtils.GetAttributeValue<bool>(item, "IsOuter"),
                CanBeDyed = XmlUtils.GetAttributeValue<bool>(item, "CanBeDyed"),
                Layer = XmlUtils.GetAttributeValue<int>(item, "Layer"),
                PlayerLevelRequired = XmlUtils.GetAttributeValue<int>(item, "PlayerLevelRequired"),
                TextureName = XmlUtils.GetAttributeValue<string>(item, "TextureName"),
                ImpactSoundsFolder = XmlUtils.GetAttributeValue<string>(item, "ImpactSoundsFolder"),
                Description = newDescription ?? string.Empty
            };
            if (clothIndex >= _clothingData.Count)
            {
                _clothingData.Count = clothIndex + 1;
            }

            _clothingData[clothIndex] = clothingData;
        }

        Num++;
        foreach (var xElement1 in item.Elements())
        {
            LoadClothingData(xElement1);
        }
    }

    public override void Initialize()
    {
        Num = 0;
        var xElement = _initializationData;
        _initializationData = null;
        if (xElement is null)
        {
            throw new InvalidOperationException("Cannot load XElement");
        }

        LoadClothingData(xElement);
        base.Initialize();
    }

    internal void SetInitializationData(XElement data)
    {
        _initializationData = new XElement(data);
    }


    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xF;
    }


    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num = (num & -3841) | ((damage & 0xF) << 8);
        return Terrain.ReplaceData(value, num);
    }

    public override bool GetCanWear(int value)
    {
        return true;
    }

    public override ClothingData GetClothingData(int data)
    {
        var num = GetClothingIndex(data);
        if (num >= 0 && num < _clothingData.Count && _clothingData[num] is not null)
        {
            return _clothingData[num]!;
        }

        for (var i = 0; i < _clothingData.Count; i++)
        {
            if (_clothingData[i] == null)
            {
                continue;
            }

            Log.Warning($"Invalid clothing index {num}, fallback to {i}.");
            return _clothingData[i]!;
        }

        throw new InvalidOperationException($"No clothing data available. Requested index={num}.");
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        IEnumerable<ClothingData?> enumerable = _clothingData.OrderBy(cd => cd?.DisplayIndex);
        foreach (var clothingData in enumerable)
        {
            if (clothingData == null)
            {
                continue;
            }

            var colorsCount = !clothingData.CanBeDyed ? 1 : 16;
            var color = 0;
            while (color < colorsCount)
            {
                var data = SetClothingColor(SetClothingIndex(0, clothingData.Index), color);
                yield return Terrain.MakeBlockValue(BlockIndex, data);
                var num = color + 1;
                color = num;
            }
        }
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain terrain,
        string[] ingredients,
        float heatLevel,
        float? playerLevel
    )
    {
        if (heatLevel < 1f)
        {
            return null;
        }

        var list = ingredients.Where(i => !string.IsNullOrEmpty(i)).ToList();
        if (list.Count == 2)
        {
            var num = 0;
            var num2 = 0;
            var num3 = 0;
            foreach (var item in list)
            {
                CraftingRecipeCatalog.DecodeIngredient(item, out var craftingId, out var data);
                if (craftingId == Catalog.Blocks[BlockIndex].CraftingId)
                {
                    num3 = Terrain.MakeBlockValue(BlockIndex, data ?? 0);
                }
                else if (craftingId == Catalog.Blocks[129].CraftingId)
                {
                    num = Terrain.MakeBlockValue(129, data ?? 0);
                }
                else if (craftingId == Catalog.Blocks[128].CraftingId)
                {
                    num2 = Terrain.MakeBlockValue(128, data ?? 0);
                }
            }

            if (num != 0 && num3 != 0)
            {
                var data2 = Terrain.ExtractData(num3);
                var clothingColor = GetClothingColor(data2);
                var clothingIndex = GetClothingIndex(data2);
                var canBeDyed = GetClothingData(data2).CanBeDyed;
                var damage = Catalog.Blocks[BlockIndex].GetDamage(num3);
                var color = PaintBucketBlock.GetColor(Terrain.ExtractData(num));
                var damage2 = Catalog.Blocks[129].GetDamage(num);
                var block = Catalog.Blocks[129];
                var block2 = Catalog.Blocks[BlockIndex];
                if (!canBeDyed)
                {
                    return null;
                }

                var num4 = PaintBucketBlock.CombineColors(clothingColor, color);
                if (num4 != clothingColor)
                {
                    return new CraftingRecipe
                    {
                        ResultCount = 1,
                        ResultValue =
                            block2.SetDamage(
                                Terrain.MakeBlockValue(
                                    BlockIndex,
                                    SetClothingIndex(SetClothingColor(0, num4), clothingIndex)
                                ),
                                damage
                            ),
                        RemainsCount = 1,
                        RemainsValue = Catalog.DamageItem(
                            Terrain.MakeBlockValue(129, color),
                            damage2 + MathUtils.Max(block.Durability / 4, 1)
                        ),
                        RequiredHeatLevel = 1f,
                        Description = new LocalizedText("BlocksManager", "Dyed", string.Empty),
                        DescriptionColor = color,
                        Ingredients = (string[])ingredients.Clone()
                    };
                }
            }

            if (num2 != 0 && num3 != 0)
            {
                var data3 = Terrain.ExtractData(num3);
                var clothingColor2 = GetClothingColor(data3);
                var clothingIndex2 = GetClothingIndex(data3);
                var canBeDyed2 = GetClothingData(data3).CanBeDyed;
                var damage3 = Catalog.Blocks[BlockIndex].GetDamage(num3);
                var damage4 = Catalog.Blocks[128].GetDamage(num2);
                var block3 = Catalog.Blocks[128];
                var block4 = Catalog.Blocks[BlockIndex];
                if (!canBeDyed2)
                {
                    return null;
                }

                if (clothingColor2 != 0)
                {
                    return new CraftingRecipe
                    {
                        ResultCount = 1,
                        ResultValue =
                            block4.SetDamage(
                                Terrain.MakeBlockValue(
                                    BlockIndex,
                                    SetClothingIndex(SetClothingColor(0, 0), clothingIndex2)
                                ),
                                damage3
                            ),
                        RemainsCount = 1,
                        RemainsValue = Catalog.DamageItem(
                            Terrain.MakeBlockValue(128, 0),
                            damage4 + MathUtils.Max(block3.Durability / 4, 1)
                        ),
                        RequiredHeatLevel = 1f,
                        Description = new LocalizedText("BlocksManager", "Not Dyed", string.Empty),
                        DescriptionSuffix = new LocalizedText("BlocksManager", "Clothes", string.Empty),
                        Ingredients = (string[])ingredients.Clone()
                    };
                }
            }
        }

        return null;
    }

    public static int GetClothingIndex(int data)
    {
        return data & 0xFF;
    }

    public static int SetClothingIndex(int data, int clothingIndex)
    {
        return (data & -256) | (clothingIndex & 0xFF);
    }

    public static int GetClothingColor(int data)
    {
        return (data >> 12) & 0xF;
    }

    public static int SetClothingColor(int data, int color)
    {
        return (data & -61441) | ((color & 0xF) << 12);
    }


    public IEnumerable<ClothingData> ClothingDefinitions => _clothingData.OfType<ClothingData>();
}
