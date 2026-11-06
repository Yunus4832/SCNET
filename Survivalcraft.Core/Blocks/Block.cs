namespace Game.Blocks;

public abstract class Block : IDisposable
{
    public static readonly BoundingBox[] DefaultCollisionBoxes = [new(Vector3.Zero, Vector3.One)];

    public bool AlignToVelocity;

    public string Behaviors = string.Empty;

    public int BlockIndex { get; internal set; } = -1;

    public BlockContentCatalog Catalog
    {
        get => field ?? throw new InvalidOperationException("Block is not bound to a content catalog.");
        internal set;
    }

    public virtual ResourceId? CollisionModelAsset => null;

    public string CraftingId = string.Empty;


    public int CreativeData;


    public int DropContent;

    public float DropCount = 1f;

    public int EmittedLightAmount;

    public float ExperienceCount;

    public bool ExplosionIncendiary;

    public float ExplosionPressure;

    public float Heat;


    public float MeleeHitProbability = 0.66f;

    public float MeleePower = 1f;

    public float NutritionalValue;

    public float ProjectilePower = 1f;

    public int RotPeriod;


    public float SicknessProbability;

    public string SoundMaterialName = string.Empty;


    public float Density = 4f;


    public BlockDigMethod DigMethod;

    public float DigResilience = 1f;

    public bool DisintegratesOnHit;


    public int Durability = -1;

    public bool ExplosionKeepsPickable;

    public float ExplosionResilience;

    public float FireDuration;


    public FoodType FoodType;

    public float FrictionFactor = 1f;

    public float FuelFireDuration;

    public float FuelHeatLevel;


    public float HackPower = 1f;

    public bool HasCollisionBehavior;


    public bool Interactive;

    public bool Aimable;

    public bool Edible;

    public bool Collidable = true;

    public bool Collapsable = false;


    public bool FluidBlocker = true;

    public bool Gatherable;

    public bool NonDuplicable;

    public bool Placeable = true;

    public bool PlacementTransparent;

    public bool DiggingTransparent;

    public virtual bool IsDiggingTransparent(int value) => DiggingTransparent;

    public bool Stickable;

    public bool Transparent;

    public bool? NonAttachable = null;

    public bool Wearable;

    public bool Editable;

    public bool KillsWhenStuck;

    public int LightAttenuation;

    public int MaxStacking = 40;

    public bool NoAutoJump;

    public bool NoSmoothRise;


    public int PlayerLevelRequired = 1;

    public float ProjectileDamping = 0.8f;

    public float ProjectileResilience = 1f;

    public float ProjectileSpeed = 15f;

    public float ProjectileStickProbability;

    public float ProjectileTipOffset;

    public float QuarryPower = 1f;

    public Random Random = new();

    public int RequiredToolLevel;

    public float ShovelPower = 1f;

    public float SleepSuitability;

    public int ToolLevel;

    public virtual bool FurnitureBuilt { get; set; } = false;


    public virtual float GetDensity(int value)
    {
        return Density;
    }


    public virtual void Dispose()
    {
    }

    public virtual void Initialize()
    {
        if (Durability is < -1 or > 65535)
        {
            throw new InvalidOperationException($"Invalid durability {Durability} for block {GetType().Name}.");
        }
    }


    public virtual float GetFuelHeatLevel(int value)
    {
        return FuelHeatLevel;
    }

    public virtual float GetExplosionResilience(int value)
    {
        return ExplosionResilience;
    }

    public virtual float GetExplosionPressure(int value)
    {
        return ExplosionPressure;
    }

    public virtual int GetMaxStacking(int value)
    {
        return MaxStacking;
    }

    public virtual bool CanAutoStack(int value1, int value2)
    {
        return value1 == value2;
    }

    public virtual float GetFuelFireDuration(int value)
    {
        return FuelFireDuration;
    }

    public virtual float GetProjectileResilience(int value)
    {
        return ProjectileResilience;
    }

    public virtual float GetFireDuration(int value)
    {
        return FireDuration;
    }

    public virtual float GetProjectileStickProbability(int value)
    {
        return ProjectileStickProbability;
    }

    public virtual bool MatchCraftingId(string craftId)
    {
        return craftId == CraftingId;
    }

    public virtual int GetPlayerLevelRequired(int value)
    {
        return PlayerLevelRequired;
    }

    public virtual bool GetHasCollisionBehavior(int value)
    {
        return HasCollisionBehavior;
    }


    public virtual bool IsEditable(int value)
    {
        return Editable;
    }

    public virtual bool IsAimable(int value)
    {
        return Aimable;
    }

    public virtual bool IsEdible(int value)
    {
        return Edible;
    }

    public virtual bool GetCanWear(int value)
    {
        return Wearable;
    }

    public virtual bool GetFurnitureBuilt(int value)
    {
        return FurnitureBuilt;
    }

    public virtual ClothingData GetClothingData(int value)
    {
        throw new InvalidOperationException($" The method '${nameof(GetClothingData)}' is not implemented.");
    }

    public virtual int GetToolLevel(int value) => ToolLevel;

    public virtual bool IsCollidable(int value) => Collidable;

    public virtual bool IsCollapsable(int value) => Collapsable;

    public virtual bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        return !IsFaceNonAttachable(subsystemTerrain, 4, value, 0);
    }

    public virtual bool IsCollapseDestructibleBlock(int value) => true;

    public virtual bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return !IsNonDuplicable(value) && IsCollidable(value);
    }

    public virtual bool IsBlockingPiston(int value) => IsCollidable(value);

    public virtual bool IsSuitableForPlants(int value, int plantValue) => false;

    public virtual bool IsTransparent(int value) => Transparent;

    public virtual bool IsNonAttachable(int value) => NonAttachable ?? IsTransparent(value);

    public virtual bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        return !IsCollidable(value) || IsNonAttachable(value);
    }

    public virtual bool IsFluidBlocker(int value) => FluidBlocker;

    public virtual bool IsGatherable(int value) => Gatherable;

    public virtual bool IsNonDuplicable(int value) => NonDuplicable;

    public virtual bool IsPlaceable(int value) => Placeable;

    public virtual bool IsPlacementTransparent(int value) => PlacementTransparent;

    public virtual bool IsStickable(int value) => Stickable;

    public virtual float GetProjectileSpeed(int value) => ProjectileSpeed;

    public virtual float GetProjectileDamping(int value) => ProjectileDamping;


    public virtual FoodType GetFoodType(int value) => FoodType;


    public virtual float GetDigResilience(int value) => DigResilience;

    public virtual BlockDigMethod GetBlockDigMethod(int value) => DigMethod;

    public virtual float GetShovelPower(int value) => ShovelPower;

    public virtual float GetQuarryPower(int value) => QuarryPower;

    public virtual float GetHackPower(int value) => HackPower;

    public virtual IEnumerable<int> GetCreativeValues()
    {
        if (CreativeData >= 0)
        {
            yield return Terrain.ReplaceContents(Terrain.ReplaceData(0, CreativeData), BlockIndex);
        }
    }

    public virtual bool GetAlignToVelocity(int value) => AlignToVelocity;

    public virtual bool IsInteractive(SubsystemTerrain subsystemTerrain, int value) => Interactive;

    public virtual IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        yield break;
    }

    public virtual CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        float? playerLevel
    )
    {
        return null;
    }

    public virtual bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value) => Transparent;


    public virtual string GetSoundMaterialName(SubsystemTerrain subsystemTerrain, int value) => SoundMaterialName;


    public virtual BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = value;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public virtual string GetCraftingId(int value) => CraftingId;


    public virtual BlockDigData GetDigValue(
        SubsystemTerrain subsystemTerrain,
        int value,
        int toolValue,
        TerrainRaycastResult raycastResult
    )
    {
        BlockDigData result = default;
        result.Value = 0;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public virtual float GetRequiredToolLevel(int value) => RequiredToolLevel;

    public virtual bool AllowsDestructionDebris(int oldValue, int newValue) => true;

    public virtual void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = AllowsDestructionDebris(oldValue, newValue);
        if (toolLevel < RequiredToolLevel)
        {
            return;
        }

        BlockDropValue item;
        if (DropContent != 0)
        {
            var num = (int)DropCount;
            if (Random.Bool(DropCount - num))
            {
                num++;
            }

            for (var i = 0; i < num; i++)
            {
                item = new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(DropContent),
                    Count = 1
                };
                dropValues.Add(item);
            }
        }

        var num2 = (int)ExperienceCount;
        if (Random.Bool(ExperienceCount - num2))
        {
            num2++;
        }

        for (var j = 0; j < num2; j++)
        {
            item = new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(248),
                Count = 1
            };
            dropValues.Add(item);
        }
    }

    public virtual int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 4) & 0xFFF;
    }

    public virtual int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num &= 0xF;
        num |= MathUtils.Clamp(damage, 0, 4095) << 4;
        return Terrain.ReplaceData(value, num);
    }

    public virtual int GetDamageDestructionValue(int value) => 0;

    public virtual int GetRotPeriod(int value) => RotPeriod;

    public virtual float GetSicknessProbability(int value) => SicknessProbability;

    public virtual float GetMeleePower(int value) => MeleePower;

    public virtual float GetMeleeHitProbability(int value) => MeleeHitProbability;

    public virtual float GetProjectilePower(int value) => ProjectilePower;

    public virtual float GetHeat(int value) => Heat;

    public virtual float GetBlockHealth(int value)
    {
        var dur = GetDurability(value);
        var dag = GetDamage(value);
        if (Durability > 0)
        {
            return (dur - dag) / (float)dur;
        }

        return -1f;
    }

    public virtual int GetDurability(int value) => Durability;

    public virtual bool GetExplosionIncendiary(int value) => ExplosionIncendiary;


    public virtual BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => DefaultCollisionBoxes;

    public virtual BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
    {
        return GetCustomCollisionBoxes(terrain, value);
    }

    public virtual int GetEmittedLightAmount(int value) => EmittedLightAmount;

    public virtual float GetNutritionalValue(int value) => NutritionalValue;

    public virtual bool ShouldAvoid(int value) => false;


    public virtual bool IsHeatBlocker(int value) => IsCollidable(value);

    public float? Raycast(
        Ray3 ray,
        SubsystemTerrain subsystemTerrain,
        int value,
        bool useInteractionBoxes,
        out int nearestBoxIndex,
        out BoundingBox nearestBox
    )
    {
        float? result = null;
        nearestBoxIndex = 0;
        nearestBox = default;
        var array = useInteractionBoxes
            ? GetCustomInteractionBoxes(subsystemTerrain, value)
            : GetCustomCollisionBoxes(subsystemTerrain, value);
        if (array.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < array.Length; i++)
        {
            var num = ray.Intersection(array[i]);
            if (!num.HasValue || (result.HasValue && !(num.Value < result.Value)))
            {
                continue;
            }

            nearestBoxIndex = i;
            result = num;
        }

        nearestBox = array[nearestBoxIndex];
        return result;
    }
}
