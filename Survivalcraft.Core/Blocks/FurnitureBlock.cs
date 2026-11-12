namespace Game.Blocks;

public class FurnitureBlock : Block, IPaintableBlock, IElectricElementBlock
{
    public int[][] FacesMaps = new int[][]
    {
        [0, 1, 2, 3, 4, 5],
        [1, 2, 3, 0, 4, 5],
        [2, 3, 0, 1, 4, 5],
        [3, 0, 1, 2, 4, 5]
    };

    public Matrix[] Matrices = new Matrix[4];

    public int[][] ReverseFacesMaps = new int[][]
    {
        [0, 1, 2, 3, 4, 5],
        [3, 0, 1, 2, 4, 5],
        [2, 3, 0, 1, 4, 5],
        [1, 2, 3, 0, 4, 5]
    };

    public ElectricElement? CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var designIndex = GetDesignIndex(Terrain.ExtractData(value));
        var design = subsystemElectricity.SubsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return null;
        }

        return design.InteractionMode switch
        {
            FurnitureInteractionMode.Multistate or FurnitureInteractionMode.ConnectedMultistate =>
                new MultistateFurnitureElectricElement(subsystemElectricity, new Point3(x, y, z)),
            FurnitureInteractionMode.ElectricButton => new ButtonFurnitureElectricElement(
                subsystemElectricity,
                new Point3(x, y, z)
            ),
            FurnitureInteractionMode.ElectricSwitch => new SwitchFurnitureElectricElement(
                subsystemElectricity,
                new Point3(x, y, z),
                value
            ),
            _ => null
        };
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var rotation = GetRotation(data);
        var designIndex = GetDesignIndex(data);
        var design = terrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return null;
        }

        var num = CellFace.OppositeFace(face < 4 ? (face - rotation + 4) % 4 : face);
        if ((design.MountingFacesMask & (1 << num)) == 0 ||
            !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
        {
            return null;
        }

        var point = CellFace.FaceToPoint3(face);
        var cellValue = terrain.Terrain.GetCellValue(x - point.X, y - point.Y, z - point.Z);
        if (Catalog.Blocks[Terrain.ExtractContents(cellValue)]
            .IsFaceTransparent(terrain, CellFace.OppositeFace(num), cellValue))
        {
            return null;
        }

        return design.InteractionMode switch
        {
            FurnitureInteractionMode.Multistate
                or FurnitureInteractionMode.ConnectedMultistate => ElectricConnectorType.Input,
            FurnitureInteractionMode.ElectricButton
                or FurnitureInteractionMode.ElectricSwitch => ElectricConnectorType.Output,
            _ => null
        };
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public int? GetPaintColor(int value)
    {
        return null;
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        if (terrain is null || !WorldRuntimeContext.For(terrain.Project).IsAuthority)
        {
            return value;
        }

        var data = Terrain.ExtractData(value);
        var designIndex = GetDesignIndex(data);
        var design = terrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return value;
        }

        var list = design.CloneChain();
        foreach (var item in list)
        {
            item.Paint(color);
        }

        var furnitureDesign = terrain.SubsystemFurnitureBlockBehavior.TryAddDesignChain(list[0], true);
        if (furnitureDesign != null)
        {
            var data2 = SetDesignIndex(
                data,
                furnitureDesign.Index,
                furnitureDesign.ShadowStrengthFactor,
                furnitureDesign.IsLightEmitter
            );
            return Terrain.ReplaceData(value, data2);
        }

        LogDesignCapacityExceeded();

        return value;
    }


    public override void Initialize()
    {
        for (var i = 0; i < 4; i++)
        {
            Matrices[i] = Matrix.CreateTranslation(new Vector3(-0.5f, 0f, -0.5f)) *
                          Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                          Matrix.CreateTranslation(new Vector3(0.5f, 0f, 0.5f));
        }

        base.Initialize();
    }


    public override bool IsFaceTransparent(SubsystemTerrain? subsystemTerrain, int face, int value)
    {
        if (subsystemTerrain == null)
        {
            return false;
        }

        var data = Terrain.ExtractData(value);
        var rotation = GetRotation(data);
        var designIndex = GetDesignIndex(data);
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design != null)
        {
            return ((1 << ReverseFacesMaps[rotation][face]) & design.TransparentFacesMask) != 0;
        }

        return false;
    }


    public override int GetEmittedLightAmount(int value)
    {
        return !GetIsLightEmitter(Terrain.ExtractData(value)) ? 0 : 15;
    }


    public override bool IsInteractive(SubsystemTerrain subsystemTerrain, int value)
    {
        var designIndex = GetDesignIndex(Terrain.ExtractData(value));
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return base.IsInteractive(subsystemTerrain, value);
        }

        if (design.InteractionMode != FurnitureInteractionMode.Multistate &&
            design.InteractionMode != FurnitureInteractionMode.ElectricButton &&
            design.InteractionMode != FurnitureInteractionMode.ElectricSwitch)
        {
            return design.InteractionMode == FurnitureInteractionMode.ConnectedMultistate;
        }

        return true;
    }

    public override string GetSoundMaterialName(SubsystemTerrain subsystemTerrain, int value)
    {
        var designIndex = GetDesignIndex(Terrain.ExtractData(value));
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return base.GetSoundMaterialName(subsystemTerrain, value);
        }

        var mainValue = design.MainValue;
        var num = Terrain.ExtractContents(mainValue);
        return Catalog.Blocks[num].GetSoundMaterialName(subsystemTerrain, mainValue);
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var designIndex = GetDesignIndex(data);
        var rotation = GetRotation(data);
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        return design != null
            ? design.GetCollisionBoxes(rotation)
            : base.GetCustomCollisionBoxes(subsystemTerrain, value);
    }

    public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var designIndex = GetDesignIndex(data);
        var rotation = GetRotation(data);
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        return design != null
            ? design.GetInteractionBoxes(rotation)
            : base.GetCustomInteractionBoxes(subsystemTerrain, value);
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var rotation = 0;
        if (raycastResult.CellFace.Face < 4)
        {
            rotation = CellFace.OppositeFace(raycastResult.CellFace.Face);
        }
        else
        {
            var forward = eyeForward;
            var num = Vector3.Dot(forward, Vector3.UnitZ);
            var num2 = Vector3.Dot(forward, Vector3.UnitX);
            var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
            var num4 = Vector3.Dot(forward, -Vector3.UnitX);
            if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 0;
            }
            else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 1;
            }
            else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 2;
            }
            else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 3;
            }
        }

        var data = SetRotation(Terrain.ExtractData(value), rotation);
        BlockPlacementData result = default;
        result.CellFace = raycastResult.CellFace;
        result.Value = Terrain.ReplaceData(value, data);
        return result;
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = true;
        var data = Terrain.ExtractData(oldValue);
        data = SetRotation(data, 0);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, data),
                Count = 1
            }
        );
    }


    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain terrain,
        string[] ingredients,
        float heatLevel,
        float? playerLevel
    )
    {
        if (!WorldRuntimeContext.For(terrain.Project).IsAuthority || heatLevel != 0f)
        {
            return null;
        }

        var num = 0;
        var num2 = 0;
        var num3 = 0;
        var list = new List<FurnitureDesign>();
        foreach (var ingredient in ingredients)
        {
            if (string.IsNullOrEmpty(ingredient))
            {
                continue;
            }

            CraftingRecipeCatalog.DecodeIngredient(ingredient, out var craftingId, out var data);
            if (craftingId == Catalog.Blocks[BlockIndex].CraftingId)
            {
                var design =
                    terrain.SubsystemFurnitureBlockBehavior.GetDesign(GetDesignIndex(data.GetValueOrDefault()));
                if (design == null)
                {
                    return null;
                }

                list.Add(design);
            }
            else if (craftingId == Catalog.GetBlock(ResourceId.Parse("game:button")).CraftingId)
            {
                num++;
            }
            else if (craftingId == Catalog.GetBlock(ResourceId.Parse("game:switch")).CraftingId)
            {
                num2++;
            }
            else
            {
                if (craftingId != Catalog.GetBlock(ResourceId.Parse("game:wire")).CraftingId)
                {
                    return null;
                }

                num3++;
            }
        }

        if (list.Count == 1 && num == 1 && num2 == 0 && num3 == 0)
        {
            var furnitureDesign = list[0].Clone();
            furnitureDesign.InteractionMode = FurnitureInteractionMode.ElectricButton;
            var furnitureDesign2 = terrain.SubsystemFurnitureBlockBehavior.TryAddDesignChain(furnitureDesign, true);
            if (furnitureDesign2 == null)
            {
                LogDesignCapacityExceeded();
                return null;
            }

            return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(
                    BlockIndex,
                    SetDesignIndex(
                        0,
                        furnitureDesign2.Index,
                        furnitureDesign2.ShadowStrengthFactor,
                        furnitureDesign2.IsLightEmitter
                    )
                ),
                ResultCount = 1,
                Description = new LocalizedText(GetType().Name, "0", string.Empty),
                Ingredients = (string[])ingredients.Clone()
            };
        }

        if (list.Count == 2 && num == 0 && num2 == 1 && num3 == 0)
        {
            var list2 = list.Select(d => d.Clone()).ToList();
            for (var j = 0; j < list2.Count; j++)
            {
                list2[j].InteractionMode = FurnitureInteractionMode.ElectricSwitch;
                list2[j].LinkedDesign = list2[(j + 1) % list2.Count];
            }

            var furnitureDesign3 = terrain.SubsystemFurnitureBlockBehavior.TryAddDesignChain(list2[0], true);
            if (furnitureDesign3 == null)
            {
                LogDesignCapacityExceeded();
                return null;
            }

            return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(
                    BlockIndex,
                    SetDesignIndex(
                        0,
                        furnitureDesign3.Index,
                        furnitureDesign3.ShadowStrengthFactor,
                        furnitureDesign3.IsLightEmitter
                    )
                ),
                ResultCount = 1,
                Description = new LocalizedText(GetType().Name, "0", string.Empty),
                Ingredients = (string[])ingredients.Clone()
            };
        }

        if (list.Count >= 2 && num == 0 && num2 == 0 && num3 <= 1)
        {
            var list3 = list.Select(d => d.Clone()).ToList();
            for (var k = 0; k < list3.Count; k++)
            {
                list3[k].InteractionMode = num3 == 0
                    ? FurnitureInteractionMode.Multistate
                    : FurnitureInteractionMode.ConnectedMultistate;
                list3[k].LinkedDesign = list3[(k + 1) % list3.Count];
            }

            var furnitureDesign4 = terrain.SubsystemFurnitureBlockBehavior.TryAddDesignChain(list3[0], true);
            if (furnitureDesign4 == null)
            {
                LogDesignCapacityExceeded();
                return null;
            }

            return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(
                    BlockIndex,
                    SetDesignIndex(
                        0,
                        furnitureDesign4.Index,
                        furnitureDesign4.ShadowStrengthFactor,
                        furnitureDesign4.IsLightEmitter
                    )
                ),
                ResultCount = 1,
                Description = new LocalizedText(GetType().Name, "0", string.Empty),
                Ingredients = (string[])ingredients.Clone()
            };
        }

        return null;
    }

    private static void LogDesignCapacityExceeded()
    {
        Log.Warning("Furniture operation could not allocate a design chain.");
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }

    public static int GetDesignIndex(int data)
    {
        return ((data >> 15) << 10) | ((data >> 2) & 1023);
    }

    public static int SetDesignIndex(int data, int designIndex, int shadowStrengthFactor, bool isLightEmitter)
    {
        var indexI3 = designIndex >> 10;
        var indexI10 = designIndex & 1023;

        data = (data & 3) | ((indexI10 & 1023) << 2); //设置旋转，和方块索引前10位
        data = (data & 4095) | ((shadowStrengthFactor & 3) << 12); //设置光强
        data = (data & 16383) | ((isLightEmitter ? 1 : 0) << 14); //设置是否发光
        data = (data & 32767) | (indexI3 << 15); //设置索引后3位

        return data;
    }

    public static FurnitureDesign? GetDesign(SubsystemFurnitureBlockBehavior subsystemFurnitureBlockBehavior, int value)
    {
        var designIndex = GetDesignIndex(Terrain.ExtractData(value));
        return subsystemFurnitureBlockBehavior.GetDesign(designIndex);
    }

    public static int GetShadowStrengthFactor(int data)
    {
        return (data >> 12) & 3;
    }

    public static bool GetIsLightEmitter(int data)
    {
        return ((data >> 14) & 1) != 0;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return true;
    }
}
