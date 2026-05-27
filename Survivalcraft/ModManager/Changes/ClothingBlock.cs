using System.Xml.Linq;

using Engine.Graphics;

using EntitySystem.XmlUtilities;

namespace Game.ModManager.Changes;

public class ClothingBlock : Block
{
    public const int Index = 203;

    private static readonly Matrix[] _slotTransforms =
    [
        Matrix.CreateTranslation(0f, -1.5f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -1.1f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -0.5f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateScale(2.7f)
    ];

    private DynamicArray<ClothingData> _clothingData = [];

    private BlockMesh _innerMesh = null!;

    private BlockMesh _outerMesh = null!;

    public int Num;

    public void LoadClothingData(XElement item)
    {
        if (item.Name.LocalName == "ClothingData")
        {
            int.TryParse(item.Attribute("Index")?.Value, out var clothIndex);
            var newDescription = item.Attribute("Description")?.Value;
            var newDisplayName = item.Attribute("DisplayName")?.Value;
            if (newDescription != null && newDescription.StartsWith('[') && newDescription.EndsWith(']') &&
                LanguageControl.TryGetBlock($"{GetType().Name}:{clothIndex}", "Description",
                    out var d))
            {
                newDescription = d;
            }

            if (newDisplayName != null && newDisplayName.StartsWith('[') && newDisplayName.EndsWith(']') &&
                LanguageControl.TryGetBlock($"{GetType().Name}:{clothIndex}", "DisplayName",
                    out var n))
            {
                newDisplayName = n;
            }

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
#if SERVER
                // Headless server only needs clothing metadata for gameplay calculations.
                Texture = null!,
#else
                Texture = ContentManager.Get<Texture2D>(XmlUtils.GetAttributeValue<string>(item, "TextureName")),
#endif
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
        XElement? xElement = null;
        ModsManager.ModListAllDo(modEntity => { modEntity.LoadClo(this, ref xElement); });
        if (xElement is null)
        {
            throw new InvalidOperationException("Cannot load XElement");
        }

        LoadClothingData(xElement);
#if !SERVER
        var playerModel = CharacterSkinsManager.GetPlayerModel(PlayerClass.Male);
        var array = new Matrix[playerModel.Bones.Count];
        playerModel.CopyAbsoluteBoneTransformsTo(array);
        var index = playerModel.FindBone("Hand1")!.Index;
        var index2 = playerModel.FindBone("Hand2")!.Index;
        array[index] = Matrix.CreateRotationY(0.1f) * array[index];
        array[index2] = Matrix.CreateRotationY(-0.1f) * array[index2];
        _innerMesh = new BlockMesh();
        foreach (var mesh in playerModel.Meshes)
        {
            var matrix = array[mesh.ParentBone!.Index];
            foreach (var meshPart in mesh.MeshParts)
            {
                var color = Color.White * 0.8f;
                color.A = byte.MaxValue;
                _innerMesh.AppendModelMeshPart(meshPart, matrix, false, false, false, false, Color.White);
                _innerMesh.AppendModelMeshPart(meshPart, matrix, false, true, false, true, color);
            }
        }

        var outerClothingModel = CharacterSkinsManager.GetOuterClothingModel(PlayerClass.Male);
        var array2 = new Matrix[outerClothingModel.Bones.Count];
        outerClothingModel.CopyAbsoluteBoneTransformsTo(array2);
        var index3 = outerClothingModel.FindBone("Leg1")!.Index;
        var index4 = outerClothingModel.FindBone("Leg2")!.Index;
        array2[index3] = Matrix.CreateTranslation(-0.02f, 0f, 0f) * array2[index3];
        array2[index4] = Matrix.CreateTranslation(0.02f, 0f, 0f) * array2[index4];
        _outerMesh = new BlockMesh();
        foreach (var mesh2 in outerClothingModel.Meshes)
        {
            var matrix2 = array2[mesh2.ParentBone!.Index];
            foreach (var meshPart2 in mesh2.MeshParts)
            {
                var color2 = Color.White * 0.8f;
                color2.A = byte.MaxValue;
                _outerMesh.AppendModelMeshPart(meshPart2, matrix2, false, false, false, false, Color.White);
                _outerMesh.AppendModelMeshPart(meshPart2, matrix2, false, true, false, true, color2);
            }
        }
#endif

        base.Initialize();
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var clothingData = GetClothingData(data);
        var clothingColor = GetClothingColor(data);
        var displayName = clothingData.DisplayName;
        return clothingColor != 0 ? SubsystemPalette.GetName(clothingColor, displayName) : displayName;
    }

    public override string GetDescription(int value)
    {
        var data = Terrain.ExtractData(value);
        var clothingData = GetClothingData(data);
        return clothingData.Description;
    }

    public override string GetCategory(int value)
    {
        if (GetClothingColor(Terrain.ExtractData(value)) == 0)
        {
            return base.GetCategory(value);
        }

        return "Dyed";
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xF;
    }

    public override int GetDisplayOrder(int value)
    {
        return GetClothingData(Terrain.ExtractData(value)).DisplayIndex;
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
        if (num >= 0 && num < _clothingData.Count && _clothingData[num] != null)
        {
            return _clothingData[num];
        }

        for (var i = 0; i < _clothingData.Count; i++)
        {
            if (_clothingData[i] == null)
            {
                continue;
            }

            Log.Warning($"Invalid clothing index {num}, fallback to {i}.");
            return _clothingData[i];
        }

        throw new InvalidOperationException($"No clothing data available. Requested index={num}.");
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        IEnumerable<ClothingData> enumerable = _clothingData.OrderBy(cd => cd.DisplayIndex);
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
                yield return Terrain.MakeBlockValue(203, 0, data);
                var num = color + 1;
                color = num;
            }
        }
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(SubsystemTerrain terrain, string[] ingredients,
        float heatLevel, ComponentPlayer? player)
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
                CraftingRecipesManager.DecodeIngredient(item, out var craftingId, out var data);
                if (craftingId == BlocksManager.Blocks[203].CraftingId)
                {
                    num3 = Terrain.MakeBlockValue(203, 0, data.HasValue ? data.Value : 0);
                }
                else if (craftingId == BlocksManager.Blocks[129].CraftingId)
                {
                    num = Terrain.MakeBlockValue(129, 0, data.HasValue ? data.Value : 0);
                }
                else if (craftingId == BlocksManager.Blocks[128].CraftingId)
                {
                    num2 = Terrain.MakeBlockValue(128, 0, data.HasValue ? data.Value : 0);
                }
            }

            if (num != 0 && num3 != 0)
            {
                var data2 = Terrain.ExtractData(num3);
                var clothingColor = GetClothingColor(data2);
                var clothingIndex = GetClothingIndex(data2);
                var canBeDyed = GetClothingData(data2).CanBeDyed;
                var damage = BlocksManager.Blocks[203].GetDamage(num3);
                var color = PaintBucketBlock.GetColor(Terrain.ExtractData(num));
                var damage2 = BlocksManager.Blocks[129].GetDamage(num);
                var block = BlocksManager.Blocks[129];
                var block2 = BlocksManager.Blocks[203];
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
                                Terrain.MakeBlockValue(203, 0,
                                    SetClothingIndex(SetClothingColor(0, num4), clothingIndex)), damage),
                        RemainsCount = 1,
                        RemainsValue = BlocksManager.DamageItem(Terrain.MakeBlockValue(129, 0, color),
                            damage2 + MathUtils.Max(block.Durability / 4, 1)),
                        RequiredHeatLevel = 1f,
                        Description =
                            $"{LanguageControl.Get("BlocksManager", "Dyed")} {SubsystemPalette.GetName(color, string.Empty)}",
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
                var damage3 = BlocksManager.Blocks[203].GetDamage(num3);
                var damage4 = BlocksManager.Blocks[128].GetDamage(num2);
                var block3 = BlocksManager.Blocks[128];
                var block4 = BlocksManager.Blocks[203];
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
                                Terrain.MakeBlockValue(203, 0,
                                    SetClothingIndex(SetClothingColor(0, 0), clothingIndex2)), damage3),
                        RemainsCount = 1,
                        RemainsValue = BlocksManager.DamageItem(Terrain.MakeBlockValue(128, 0, 0),
                            damage4 + MathUtils.Max(block3.Durability / 4, 1)),
                        RequiredHeatLevel = 1f,
                        Description = LanguageControl.Get("BlocksManager", "Not Dyed") + " " +
                                      LanguageControl.Get("BlocksManager", "Clothes"),
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

    public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value,
        int x, int y, int z)
    {
    }

    public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size,
        ref Matrix matrix, DrawBlockEnvironmentData environmentData)
    {
        var data = Terrain.ExtractData(value);
        var clothingColor = GetClothingColor(data);
        var clothingData = GetClothingData(data);
        var matrix2 = _slotTransforms[(int)clothingData.Slot] * Matrix.CreateScale(size) * matrix;
        if (clothingData.IsOuter)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, _outerMesh, clothingData.Texture,
                color * SubsystemPalette.GetFabricColor(environmentData, clothingColor), 1f, ref matrix2,
                environmentData);
        }
        else
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, _innerMesh, clothingData.Texture,
                color * SubsystemPalette.GetFabricColor(environmentData, clothingColor), 1f, ref matrix2,
                environmentData);
        }
    }
}
