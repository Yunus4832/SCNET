using System.Globalization;

using Engine.Graphics;

namespace Game.Blocks;

public class EggBlock : Block
{
    public const int Index = 118;

    public const string TypeName = "EggBlock";

    public readonly DynamicArray<EggType> EggTypes = [];

    public ReadOnlyList<EggType> ReadOnlyEggTypes => new(EggTypes);

    public override void Initialize()
    {
        EggTypes.Clear();
        var parameterSetType = DatabaseManager.GameDatabase.ParameterSetType;
        var eggParameterSetGuid = new Guid("300ff557-775f-4c7c-a88a-26655369f00b");
        var eggItems = from o in DatabaseManager
                .GameDatabase
                .Database
                .Root
                .GetExplicitNestingChildren(parameterSetType, false)
            where o.EffectiveInheritanceRoot.Guid == eggParameterSetGuid
            select o;
        foreach (var item in eggItems)
        {
            var nestedValue = item.GetNestedValue<int>("EggTypeIndex");

            if (nestedValue < 0)
            {
                continue;
            }

            var value = item.GetNestedValue<string>("DisplayName");
            string? displayNameKey = null;
            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                var lp = value.Substring(1, value.Length - 2).Split([":"], StringSplitOptions.RemoveEmptyEntries);
                displayNameKey = lp.Length > 1 ? lp[1] : null;
                if (displayNameKey is not null)
                {
                    value = LanguageManager.GetDatabase("DisplayName", displayNameKey);
                }
            }

            if (nestedValue >= EggTypes.Count)
            {
                EggTypes.Count = nestedValue + 1;
            }

            EggTypes[nestedValue] = new EggType
            {
                EggTypeIndex = nestedValue,
                ShowEgg = item.GetNestedValue<bool>("ShowEgg"),
                DisplayName = value,
                DisplayNameKey = displayNameKey,
                TemplateName = item.NestingParent?.Name ?? string.Empty,
                NutritionalValue = item.GetNestedValue<float>("NutritionalValue"),
                Color = item.GetNestedValue<Color>("Color"),
                ScaleUv = item.GetNestedValue<Vector2>("ScaleUV"),
                SwapUv = item.GetNestedValue<bool>("SwapUV"),
                Scale = item.GetNestedValue<float>("Scale"),
                TextureSlot = item.GetNestedValue<int>("TextureSlot"),
                BlockMesh = new BlockMesh()
            };
        }

        var model = ContentManager.Get<Model>("Models/Egg");
        var eggMesh = model.FindMesh("Egg")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            eggMesh.ParentBone ??
            throw new InvalidOperationException("Required EggMesh.ParentBone is null")
        );
        foreach (var eggType in EggTypes.OfType<EggType>())
        {
            eggType.BlockMesh.AppendModelMeshPart(eggMesh.MeshParts[0], boneAbsoluteTransform,
                false, false, false, false,
                eggType.Color);
            var identity = Matrix.Identity;
            if (eggType.SwapUv)
            {
                identity.M11 = 0f;
                identity.M12 = 1f;
                identity.M21 = 1f;
                identity.M22 = 0f;
            }

            identity *= Matrix.CreateScale(0.0625f * eggType.ScaleUv.X, 0.0625f * eggType.ScaleUv.Y, 1f);
            identity *= Matrix.CreateTranslation(eggType.TextureSlot % 16 / 16f, eggType.TextureSlot / 16 / 16f,
                0f);
            eggType.BlockMesh.TransformTextureCoordinates(identity);
        }

        base.Initialize();
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var eggType = GetEggType(Terrain.ExtractData(value));
        var data = Terrain.ExtractData(value);
        var isCooked = GetIsCooked(data);
        var isLaid = GetIsLaid(data);
        if (isCooked)
        {
            return LanguageManager.Get(TypeName, 1) + eggType.DisplayName;
        }

        if (!isLaid)
        {
            return eggType.DisplayName;
        }

        return LanguageManager.Get(TypeName, 2) + eggType.DisplayName;
    }

    public override string GetCategory(int value)
    {
        return "Spawner Eggs";
    }

    public override string GetDescription(int value)
    {
        var eggType = GetEggType(Terrain.ExtractData(value));
        var displayName = EggTypes[eggType.EggTypeIndex].DisplayName;
        return LanguageManager.Get(TypeName, 3) + displayName[..^1];
    }

    public override float GetNutritionalValue(int value)
    {
        var eggType = GetEggType(Terrain.ExtractData(value));
        if (!GetIsCooked(Terrain.ExtractData(value)))
        {
            return eggType.NutritionalValue;
        }

        return 1.5f * eggType.NutritionalValue;
    }

    public override float GetSicknessProbability(int value)
    {
        return !GetIsCooked(Terrain.ExtractData(value)) ? SicknessProbability : 0f;
    }

    public override int GetRotPeriod(int value)
    {
        return GetNutritionalValue(value) > 0f ? base.GetRotPeriod(value) : 0;
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 16) & 1;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num = (num & -65537) | ((damage & 1) << 16);
        return Terrain.ReplaceData(value, num);
    }

    public override int GetDamageDestructionValue(int value)
    {
        return 246;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        foreach (var eggType in EggTypes.OfType<EggType>())
        {
            if (!eggType.ShowEgg)
            {
                continue;
            }

            yield return Terrain.MakeBlockValue(118, 0, SetEggType(0, eggType.EggTypeIndex));
            if (eggType.NutritionalValue > 0f)
            {
                yield return Terrain.MakeBlockValue(118, 0, SetIsCooked(SetEggType(0, eggType.EggTypeIndex), true));
            }
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        foreach (var eggType in ReadOnlyEggTypes.OfType<EggType>())
        {
            if (!(eggType.NutritionalValue > 0f))
            {
                continue;
            }

            var rot = 0;
            while (rot <= 1)
            {
                var craftingRecipe = new CraftingRecipe
                {
                    ResultCount = 1,
                    ResultValue = Terrain.MakeBlockValue(118, 0,
                        SetEggType(SetIsCooked(0, true), eggType.EggTypeIndex)),
                    RemainsCount = 1,
                    RemainsValue = Terrain.MakeBlockValue(91),
                    RequiredHeatLevel = 1f,
                    Description = LanguageManager.Get(TypeName, 4)
                };
                var data = SetEggType(SetIsLaid(0, true), eggType.EggTypeIndex);
                var value = SetDamage(Terrain.MakeBlockValue(118, 0, data), rot);
                craftingRecipe.Ingredients[0] =
                    "egg:" + Terrain.ExtractData(value).ToString(CultureInfo.InvariantCulture);
                craftingRecipe.Ingredients[1] = "waterbucket";
                yield return craftingRecipe;
                var num = rot + 1;
                rot = num;
            }
        }
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var data = Terrain.ExtractData(value);
        var eggType = GetEggType(data);
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            eggType.BlockMesh,
            color,
            eggType.Scale * size,
            ref matrix,
            environmentData
        );
    }

    public EggType GetEggType(int data)
    {
        var index = (data >> 4) & 0xFFF;
        return EggTypes[index];
    }

    public EggType? GetEggTypeByCreatureTemplateName(string templateName)
    {
        return EggTypes.FirstOrDefault(e => e.TemplateName == templateName);
    }

    public static bool GetIsCooked(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetIsCooked(int data, bool isCooked)
    {
        if (!isCooked)
        {
            return data & -2;
        }

        return data | 1;
    }

    public static bool GetIsLaid(int data)
    {
        return (data & 2) != 0;
    }

    public static int SetIsLaid(int data, bool isLaid)
    {
        if (!isLaid)
        {
            return data & -3;
        }

        return data | 2;
    }

    public static int SetEggType(int data, int eggTypeIndex)
    {
        data &= -65521;
        data |= (eggTypeIndex & 0xFFF) << 4;
        return data;
    }

    public class EggType
    {
        public required BlockMesh BlockMesh;

        public Color Color;

        public string? DisplayNameKey;

        public string DisplayName
        {
            get => DisplayNameKey is null ? field : LanguageManager.GetDatabase("DisplayName", DisplayNameKey);
            set;
        } = string.Empty;

        public int EggTypeIndex;

        public float NutritionalValue;

        public float Scale;

        public Vector2 ScaleUv;

        public bool ShowEgg;

        public bool SwapUv;

        public string TemplateName = string.Empty;

        public int TextureSlot;
    }
}
