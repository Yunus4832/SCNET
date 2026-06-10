using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemCreatureSpawn : Subsystem, IUpdateable
{
    private static readonly SpawnLocationType[] _spawnLocations =
        EnumUtils.GetEnumValues(typeof(SpawnLocationType)).Cast<SpawnLocationType>().ToArray();

    private static readonly int _areaLimit = SettingsManager.CreatureAreaLimit;

    private static readonly int _maxPlayerAreaLimit = SettingsManager.CreatureMaxPlayerAreaLimit;

    private static readonly int _maxPointLimit = SettingsManager.CreatureMaxPointLimit;

    private static readonly int _areaRadius = SettingsManager.CreatureAreaRadius;

    private static readonly int _totalLimitConstant = SettingsManager.CreatureTotalLimitConstant;

    private static readonly int _areaLimitConstant = SettingsManager.CreatureAreaLimitConstant;

    private static readonly int _areaRadiusConstant = SettingsManager.CreatureAreaRadiusConstant;

    private static readonly float _spawnIntervalTime = SettingsManager.CreatureSpawnIntervalTime;

    private static readonly float _constantSpawnIntervalTime = SettingsManager.CreatureConstantSpawnIntervalTime;

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private readonly Dictionary<ComponentCreature, bool> _creatures = new();

    private readonly List<CreatureType> _creatureTypes = [];

    private readonly List<SpawnChunk> _newSpawnChunks = [];

    private readonly Random _random = new();

    private readonly List<SpawnChunk> _spawnChunks = [];

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemSpawn _subsystemSpawn = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    private static int TotalLimit => SettingsManager.CreatureTotalLimit;

    public Dictionary<ComponentCreature, bool>.KeyCollection Creatures => _creatures.Keys;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != EnvironmentBehaviorMode.Living)
        {
            return;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(_constantSpawnIntervalTime, 0))
        {
            if (_newSpawnChunks.Count > 0)
            {
                _newSpawnChunks.RandomShuffle(max => _random.Int(0, max - 1));
                foreach (var newSpawnChunk in _newSpawnChunks)
                {
                    SpawnChunkCreatures(newSpawnChunk, GetSpawnFactorByPlayerCount(), false);
                }

                _newSpawnChunks.Clear();
            }

            if (_spawnChunks.Count > 0)
            {
                _spawnChunks.RandomShuffle(max => _random.Int(0, max - 1));
                foreach (var spawnChunk in _spawnChunks)
                {
                    SpawnChunkCreatures(spawnChunk, 2, true);
                }

                _spawnChunks.Clear();
            }
        }

        if (_subsystemTime.PeriodicGameTimeEvent(_spawnIntervalTime, 2.0))
        {
            SpawnRandomCreature();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        InitializeCreatureTypes();

        _subsystemSpawn.SpawningChunk += delegate(SpawnChunk chunk)
        {
            _spawnChunks.Add(chunk);
            if (!chunk.IsSpawned)
            {
                _newSpawnChunks.Add(chunk);
            }
        };
    }

    public void CreateEntity(
        string templateName,
        Vector3 position,
        ComponentMiner? componentMiner,
        SubsystemAudio? subsystemAudio
    )
    {
        if (string.IsNullOrEmpty(templateName))
        {
            throw new ArgumentException(nameof(templateName));
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var entity = DatabaseManager.CreateEntity(Project, templateName, true)!;
        var componentFrame = entity.FindComponent<ComponentFrame>(true)!;
        componentFrame.Position = position;
        componentFrame.Rotation =
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, _random.Float(0f, (float)Math.PI * 2f));
        entity.FindComponent<ComponentSpawn>(true)!.SpawnDuration = 0f;
        Project.AddEntity(entity);
        componentMiner?.RemoveActiveTool(1);
        subsystemAudio?.PlaySound("Audio/BlockPlaced", 1f, 0f, position, 3f, true);
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentCreature>())
        {
            if (item != null)
            {
                _creatures.Add(item, true);
            }
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentCreature>())
        {
            if (item != null)
            {
                _creatures.Remove(item);
            }
        }
    }

    private void InitializeCreatureTypes()
    {
        _creatureTypes.Add(new CreatureType("Duck", SpawnLocationType.Surface, true, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num97 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity26 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature38 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num98 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var topHeight3 = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                return humidity26 > 8 && temperature38 > 4 && num97 > 30f && point.Y >= topHeight3 &&
                       (BlocksManager.Blocks[num98] is LeavesBlock || num98 == 18 || num98 == 8 || num98 == 2)
                    ? 2.5f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Duck", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Raven", SpawnLocationType.Surface, true, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num95 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature37 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity25 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num96 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var topHeight2 = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                return (humidity25 <= 8 || temperature37 <= 4) && num95 > 30f && point.Y >= topHeight2 &&
                       (BlocksManager.Blocks[num96] is LeavesBlock || num96 == 62 || num96 == 8 || num96 == 2 ||
                        num96 == 7)
                    ? 2.5f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Raven", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Seagull", SpawnLocationType.Surface, true, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num93 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var num94 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                return num93 is > -100f and < 40f && point.Y >= topHeight &&
                       num94 is 18 or 7 or 6 or 62
                    ? 2.5f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Seagull", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Wildboar", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num91 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity24 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num92 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num91 > 20f && humidity24 > 8 && point.Y < 80 && (num92 == 8 || num92 == 2) ? 0.25f : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Wildboar", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Brown Cattle", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num89 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity23 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature36 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num90 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num89 > 20f && humidity23 > 4 && temperature36 >= 8 && point.Y < 70 && (num90 == 8 || num90 == 2)
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var num87 = _random.Int(3, 5);
                var num88 = MathUtils.Min(_random.Int(1, 3), num87);
                var count2 = num87 - num88;
                return 0 + SpawnCreatures(creatureType, "Bull_Brown", point, num88).Count +
                       SpawnCreatures(creatureType, "Cow_Brown", point, count2).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Black Cattle", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num85 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity22 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature35 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num86 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num85 > 20f && humidity22 > 4 && temperature35 < 8 && point.Y < 70 && (num86 == 8 || num86 == 2)
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var num83 = _random.Int(3, 5);
                var num84 = MathUtils.Min(_random.Int(1, 3), num83);
                var count = num83 - num84;
                return 0 + SpawnCreatures(creatureType, "Bull_Black", point, num84).Count +
                       SpawnCreatures(creatureType, "Cow_Black", point, count).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("White Bull", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num81 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity21 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature34 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num82 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num81 > 20f && humidity21 > 8 && temperature34 < 4 && point.Y < 70 && (num82 == 8 || num82 == 2)
                    ? 0.01f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bull_White", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Gray Wolves", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num79 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity20 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num80 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num79 > 40f && humidity20 >= 8 && point.Y < 100 && (num80 == 8 || num80 == 2) ? 0.075f : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Wolf_Gray", point, _random.Int(1, 3)).Count
        });
        _creatureTypes.Add(new CreatureType("Coyotes", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num77 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity19 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature33 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num78 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num77 > 40f && temperature33 > 8 && humidity19 is < 8 and >= 2 && point.Y < 100 &&
                       num78 == 7
                    ? 0.075f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Wolf_Coyote", point, _random.Int(1, 3)).Count
        });
        _creatureTypes.Add(new CreatureType("Brown Bears", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num75 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature32 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity18 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num76 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num75 > 40f && humidity18 >= 4 && temperature32 >= 8 && point.Y < 110 &&
                       num76 is 8 or 2 or 3
                    ? 0.1f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Brown", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Black Bears", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num73 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature31 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity17 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num74 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num73 > 40f && humidity17 >= 4 && temperature31 < 8 && point.Y < 120 &&
                       num74 is 8 or 2 or 3
                    ? 0.1f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Polar Bears", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num71 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature30 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num72 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num71 > -40f && temperature30 < 8 && point.Y < 80 && num72 == 62 ? 0.1f : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Polar", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Horses", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num69 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature29 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity16 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num70 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num69 > 20f && temperature29 > 3 && humidity16 > 6 && point.Y < 80 &&
                       num70 is 8 or 2 or 3
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var temperature28 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num68 = 0;
                if (_random.Float(0f, 1f) < 0.35f)
                {
                    num68 += SpawnCreatures(creatureType, "Horse_Black", point, 1).Count;
                }

                if (_random.Float(0f, 1f) < 0.5f)
                {
                    num68 += SpawnCreatures(creatureType, "Horse_Bay", point, 1).Count;
                }

                if (_random.Float(0f, 1f) < 0.5f)
                {
                    num68 += SpawnCreatures(creatureType, "Horse_Chestnut", point, 1).Count;
                }

                if (temperature28 > 8 && _random.Float(0f, 1f) < 0.3f)
                {
                    num68 += SpawnCreatures(creatureType, "Horse_Palomino", point, 1).Count;
                }

                if (temperature28 < 8 && _random.Float(0f, 1f) < 0.3f)
                {
                    num68 += SpawnCreatures(creatureType, "Horse_White", point, 1).Count;
                }

                return num68;
            }
        });
        _creatureTypes.Add(new CreatureType("Camels", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num66 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature27 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity15 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num67 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num66 > 20f && temperature27 > 8 && humidity15 < 8 && point.Y < 80 && num67 == 7 ? 0.05f : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Camel", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Donkeys", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num64 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature26 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num65 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num64 > 20f && temperature26 > 6 && point.Y < 120 &&
                       num65 is 8 or 2 or 3 or 7
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Donkey", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Giraffes", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num62 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature25 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity14 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num63 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num62 > 20f && temperature25 > 8 && humidity14 > 7 && point.Y < 75 &&
                       num63 is 8 or 2 or 3
                    ? 0.03f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Giraffe", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Rhinos", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num60 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature24 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity13 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num61 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num60 > 40f && temperature24 > 8 && humidity13 > 7 && point.Y < 75 &&
                       num61 is 8 or 2 or 3
                    ? 0.04f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Rhino", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Tigers", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num58 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity12 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num59 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num58 > 40f && humidity12 > 8 && point.Y < 80 &&
                       num59 is 8 or 2 or 3 or 7
                    ? 0.025f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("White Tigers", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num56 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature23 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num57 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num56 > 40f && temperature23 < 2 && point.Y < 90 &&
                       num57 is 8 or 2 or 3 or 7 or 62
                    ? 0.025f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger_White", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Lions", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num54 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature22 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num55 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num54 > 40f && temperature22 > 8 && point.Y < 80 &&
                       num55 is 8 or 2 or 3 or 7
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Lion", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Jaguars", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num52 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature21 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity11 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num53 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num52 > 40f && humidity11 > 8 && temperature21 > 8 && point.Y < 100 &&
                       num53 is 8 or 2 or 3 or 7 or 12
                    ? 0.04f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Leopards", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num50 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature20 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num51 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num50 > 40f && temperature20 > 8 && point.Y < 120 &&
                       num51 is 8 or 2 or 3 or 7 or 12
                    ? 0.04f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Zebras", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num48 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature19 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity10 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num49 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num48 > 20f && temperature19 > 8 && humidity10 > 7 && point.Y < 80 &&
                       num49 is 8 or 2 or 3
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Zebra", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Gnus", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num46 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature18 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num47 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num46 > 20f && temperature18 > 8 && point.Y < 80 && (num47 == 8 || num47 == 2 || num47 == 3)
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Gnu", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Reindeers", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var temperature17 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num45 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return temperature17 < 3 && point.Y < 90 && (num45 == 8 || num45 == 2 || num45 == 3 || num45 == 62)
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Reindeer", point, _random.Int(1, 3)).Count
        });
        _creatureTypes.Add(new CreatureType("Mooses", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var temperature16 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num44 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return temperature16 < 7 && point.Y < 90 && (num44 == 8 || num44 == 2 || num44 == 3 || num44 == 62)
                    ? 0.1f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Moose", point, _random.Int(1, 1)).Count
        });
        _creatureTypes.Add(new CreatureType("Bisons", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var temperature15 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity9 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num43 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return temperature15 < 10 && humidity9 < 12 && point.Y < 80 &&
                       num43 is 8 or 2 or 3 or 62
                    ? 0.1f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Bison", point, _random.Int(1, 4)).Count
        });
        _creatureTypes.Add(new CreatureType("Ostriches", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num41 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature14 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity8 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num42 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num41 > 20f && temperature14 > 8 && humidity8 < 8 && point.Y < 75 &&
                       num42 is 8 or 2 or 7
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Ostrich", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Cassowaries", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num39 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature13 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity7 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num40 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num39 > 20f && temperature13 > 8 && humidity7 < 12 && point.Y < 75 &&
                       num40 is 8 or 2 or 7
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Cassowary", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Hyenas", SpawnLocationType.Surface, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num37 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature12 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num38 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num37 > 40f && temperature12 > 8 && point.Y < 80 && (num38 == 8 || num38 == 2 || num38 == 7)
                    ? 0.05f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Hyena", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Cave Bears", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num36 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num36 is 3 or 67 or 4 or 66 or 2 or 7 ? 1f : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var templateName11 = _random.Int(0, 1) == 0 ? "Bear_Black" : "Bear_Brown";
                return SpawnCreatures(creatureType, templateName11, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Cave Tigers", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num35 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num35 is 3 or 67 or 4 or 66 or 2 or 7 ? 0.25f : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Cave Lions", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var temperature11 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity6 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num34 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num34 is 3 or 67 or 4 or 66 or 2 or 7 &&
                       temperature11 > 8 && humidity6 < 8
                    ? 0.25f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Lion", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Cave Jaguars", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num33 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num33 is 3 or 67 or 4 or 66 or 2 or 7 ? 0.5f : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Cave Leopards", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num32 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num32 is 3 or 67 or 4 or 66 or 2 or 7 ? 0.25f : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Cave Hyenas", SpawnLocationType.Cave, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var temperature10 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num31 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                return num31 is 3 or 67 or 4 or 66 or 2 or 7 &&
                       temperature10 > 8
                    ? 1f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Hyena", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Bull Sharks", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                    ? 0f
                    : 0.4f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName10 = "Shark_Bull";
                return SpawnCreatures(creatureType, templateName10, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Tiger Sharks", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < -5f)
                    ? 0f
                    : 0.3f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName9 = "Shark_Tiger";
                return SpawnCreatures(creatureType, templateName9, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Great White Sharks", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < -20f)
                    ? 0f
                    : 0.2f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName8 = "Shark_GreatWhite";
                return SpawnCreatures(creatureType, templateName8, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Barracudas", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                    ? 0f
                    : 0.5f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName7 = "Barracuda";
                return SpawnCreatures(creatureType, templateName7, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Bass_Sea", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                    ? 0f
                    : 1f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName6 = "Bass_Sea";
                return SpawnCreatures(creatureType, templateName6, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Bass_Freshwater", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num30 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature9 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                return num30 > 10f && temperature9 >= 4 ? 1f : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName5 = "Bass_Freshwater";
                return SpawnCreatures(creatureType, templateName5, point, _random.Int(1, 2)).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Rays", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = (_, point) =>
                !(_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z) < 10f)
                    ? 1f
                    : 0.5f,
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var num27 = 0;
                var num28 = 0;
                for (var i = point.X - 2; i <= point.X + 2; i++)
                for (var j = point.Z - 2; j <= point.Z + 2; j++)
                {
                    if (_subsystemTerrain.Terrain.GetCellContents(point.X, point.Y, point.Z) == 18)
                    {
                        for (var num29 = point.Y - 1; num29 > 0; num29--)
                        {
                            switch (_subsystemTerrain.Terrain.GetCellContents(point.X, num29, point.Z))
                            {
                                case 2:
                                    num27++;
                                    break;
                                case 7:
                                    num28++;
                                    break;
                                default:
                                    continue;
                            }

                            break;
                        }
                    }
                }

                var templateName4 = num27 >= num28 ? "Ray_Brown" : "Ray_Yellow";
                return SpawnCreatures(creatureType, templateName4, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Piranhas", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num26 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity5 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var temperature8 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                return num26 > 10f && humidity5 >= 4 && temperature8 >= 7 ? 1f : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName3 = "Piranha";
                return SpawnCreatures(creatureType, templateName3, point, _random.Int(2, 4)).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Orcas", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num25 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                if (num25 < -100f)
                {
                    return 0.05f;
                }

                return num25 < -20f ? 0.01f : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName2 = "Orca";
                return SpawnCreatures(creatureType, templateName2, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Belugas", SpawnLocationType.Water, false, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num24 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                if (num24 < -100f)
                {
                    return 0.05f;
                }

                return num24 < -20f ? 0.01f : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                const string templateName = "Beluga";
                return SpawnCreatures(creatureType, templateName, point, 1).Count;
            }
        });
        _creatureTypes.Add(new CreatureType("Constant Gray Wolves", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num21 =
                    _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var humidity4 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                float num22 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num23 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast10 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (((num21 > 20f && humidity4 >= 8) || (num22 <= 8f && point.Y < 90 && cellLightFast10 <= 7)) &&
                    num23 is 8 or 2)
                {
                    return 2f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Wolf_Gray", point, _random.Int(1, 3)).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Coyotes", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num17 =
                    _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                float num18 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                float num19 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num20 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast9 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num17 > 20f && num19 > 8f && num18 < 8f && point.Y < 90 && cellLightFast9 <= 7 &&
                    num20 == 7)
                {
                    return 2f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Wolf_Coyote", point, _random.Int(1, 3)).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Brown Bears", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num15 =
                    _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature7 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity3 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num16 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast8 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num15 > 20f && humidity3 >= 4 && temperature7 >= 8 && point.Y < 100 && cellLightFast8 <= 7 &&
                    num16 is 8 or 2 or 3)
                {
                    return 0.5f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Brown", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Black Bears", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num13 =
                    _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature6 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num14 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast7 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num13 > 20f && temperature6 < 8 && point.Y < 110 && cellLightFast7 <= 7 &&
                    num14 is 8 or 2 or 3)
                {
                    return 0.5f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Polar Bears", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num11 =
                    _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature5 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num12 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast6 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num11 > -40f && temperature5 < 8 && point.Y < 90 && cellLightFast6 <= 7 && num12 == 62)
                {
                    return 0.25f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Tigers", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num9 = _subsystemTerrain.TerrainContentsGenerator
                    .CalculateOceanShoreDistance(point.X, point.Z);
                var humidity2 = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num10 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast5 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num9 > 20f && humidity2 > 8 && point.Y < 90 && cellLightFast5 <= 7 &&
                    num10 is 8 or 2 or 3)
                {
                    return 0.05f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Lions", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num7 = _subsystemTerrain.TerrainContentsGenerator
                    .CalculateOceanShoreDistance(point.X, point.Z);
                var temperature4 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num8 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast4 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num7 > 20f && temperature4 > 8 && point.Y < 90 && cellLightFast4 <= 7 &&
                    num8 is 8 or 2 or 3 or 7)
                {
                    return 0.25f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Lion", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Jaguars", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num5 = _subsystemTerrain.TerrainContentsGenerator
                    .CalculateOceanShoreDistance(point.X, point.Z);
                var temperature3 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num6 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast3 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num5 > 20f && temperature3 > 8 && humidity > 8 && point.Y < 100 && cellLightFast3 <= 7 &&
                    num6 is 8 or 2 or 3 or 12)
                {
                    return 0.25f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Leopards", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num3 = _subsystemTerrain.TerrainContentsGenerator
                    .CalculateOceanShoreDistance(point.X, point.Z);
                var temperature2 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num4 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast2 = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num3 > 20f && temperature2 > 8 && point.Y < 110 && cellLightFast2 <= 7 &&
                    num4 is 8 or 2 or 3 or 12)
                {
                    return 0.25f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Constant Hyenas", SpawnLocationType.Surface, false, true)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                {
                    return 0f;
                }

                var num = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                var num2 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var cellLightFast = _subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
                if (num > 20f && temperature > 8 && point.Y < 100 && cellLightFast <= 7 &&
                    num2 is 8 or 2 or 3 or 7)
                {
                    return 1f;
                }

                return 0f;
            },
            SpawnFunction = (creatureType, point) =>
                SpawnCreatures(creatureType, "Hyena", point, _random.Int(1, 2)).Count
        });
        _creatureTypes.Add(new CreatureType("Pigeon", SpawnLocationType.Surface, true, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num95 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature38 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num96 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var topHeight2 = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                return temperature38 > 3 && num95 > 30f && point.Y >= topHeight2 &&
                       (BlocksManager.Blocks[num96] is LeavesBlock || num96 == 8 || num96 == 2 || num96 == 7)
                    ? 1.5f
                    : 0f;
            },
            SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Pigeon", point, 1).Count
        });
        _creatureTypes.Add(new CreatureType("Sparrow", SpawnLocationType.Surface, true, false)
        {
            SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
            {
                var num93 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                var temperature37 = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                var num94 = Terrain.ExtractContents(
                    _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                return temperature37 > 3 && num93 > 20f && point.Y >= topHeight &&
                       (BlocksManager.Blocks[num94] is LeavesBlock || num94 == 8 || num94 == 2 || num94 == 7)
                    ? 2f
                    : 0f;
            },
            SpawnFunction = delegate(CreatureType creatureType, Point3 point)
            {
                var count3 = _random.Int(1, 2);
                return SpawnCreatures(creatureType, "Sparrow", point, count3).Count;
            }
        });
    }

    private void SpawnRandomCreature()
    {
        if (GetSpawnFactorByPlayerCount() == 0)
        {
            return;
        }

        //判断全区块生物总数限制
        if (CountCreatures(false) >= TotalLimit)
        {
            return;
        }

        var centers = new List<Vector3>();
        foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
        {
            centers.Add(componentPlayer.ComponentBody.Position);
        }

        foreach (var center in centers)
        {
            var v = new Vector2(center.X, center.Z);
            if (CountCreaturesInArea(v - new Vector2(60f), v + new Vector2(60f), false) >= _maxPlayerAreaLimit)
            {
                break;
            }

            var spawnLocationType = GetRandomSpawnLocationType();
            var spawnPoint = GetRandomSpawnPoint(center, spawnLocationType);
            if (!spawnPoint.HasValue)
            {
                continue;
            }

            var c2 = new Vector2(spawnPoint.Value.X, spawnPoint.Value.Z) - new Vector2(16f);
            var c3 = new Vector2(spawnPoint.Value.X, spawnPoint.Value.Z) + new Vector2(16f);
            if (CountCreaturesInArea(c2, c3, false) >= _maxPointLimit)
            {
                break;
            }

            var source = _creatureTypes.Where(c => c.SpawnLocationType == spawnLocationType && c.RandomSpawn);
            var creatureTypes = source as CreatureType[] ?? source.ToArray();
            var items = creatureTypes.Select(c => c.SpawnSuitabilityFunction(c, spawnPoint.Value));
            var randomWeightedItem = GetRandomWeightedItem(items);
            if (randomWeightedItem < 0)
            {
                continue;
            }

            var creatureType = creatureTypes.ElementAt(randomWeightedItem);
            creatureType.SpawnFunction(creatureType, spawnPoint.Value);
        }
    }

    private Point3? GetRandomSpawnPoint(Vector3 center, SpawnLocationType spawnLocationType)
    {
        for (var i = 0; i < 10; i++)
        {
            var x = Terrain.ToCell(center.X) + _random.Sign() * _random.Int(20, 40);
            var y = MathUtils.Clamp(Terrain.ToCell(center.Y) + _random.Int(-30, 30), 2, 253);
            var z = Terrain.ToCell(center.Z) + _random.Sign() * _random.Int(20, 40);
            var result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
            if (result.HasValue)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// </summary>
    /// <param name="chunk">区块</param>
    /// <param name="maxAttempts">最大尝试次数</param>
    /// <param name="constantSpawn">true 是新生成 false 是旧生成(上次生成由于数量限制等原因导致没有生成)</param>
    private void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
    {
        if (GetSpawnFactorByPlayerCount() == 0)
        {
            return;
        }

        //判断这个区块的生物数量限制
        var num = constantSpawn ? _totalLimitConstant : TotalLimit;
        var num2 = constantSpawn ? _areaLimitConstant : _areaLimit;
        float v = constantSpawn ? _areaRadiusConstant : _areaRadius;
        var num3 = CountCreatures(constantSpawn);
        var c2 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(v);
        var c3 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(v);
        var num4 = CountCreaturesInArea(c2, c3, constantSpawn);
        for (var i = 0; i < maxAttempts; i++)
        {
            if (num3 >= num) //总数超出，不生成
            {
                break;
            }

            if (num4 >= num2) //区块超出，不生成
            {
                break;
            }

            var spawnLocationType = GetRandomSpawnLocationType();
            var spawnPoint = GetRandomChunkSpawnPoint(chunk, spawnLocationType);
            if (!spawnPoint.HasValue)
            {
                continue;
            }

            var source = _creatureTypes.Where(c =>
                c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn);
            var creatureTypes = source as CreatureType[] ?? source.ToArray();
            var items = creatureTypes.Select(c => c.SpawnSuitabilityFunction(c, spawnPoint.Value));
            var randomWeightedItem = GetRandomWeightedItem(items);
            if (randomWeightedItem < 0)
            {
                continue;
            }

            var creatureType = creatureTypes.ElementAt(randomWeightedItem);
            var num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
            num3 += num5;
            num4 += num5;
        }
    }

    private List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
    {
        var list = new List<Entity>();
        var num = 0;
        while (count > 0 && num < 50)
        {
            var spawnPoint = point;
            if (num > 0)
            {
                spawnPoint.X += _random.Int(-8, 8);
                spawnPoint.Y += _random.Int(-4, 8);
                spawnPoint.Z += _random.Int(-8, 8);
            }

            var point2 = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
            if (point2.HasValue && creatureType.SpawnSuitabilityFunction(creatureType, point2.Value) > 0f)
            {
                var position = new Vector3(point2.Value.X + _random.Float(0.4f, 0.6f), point2.Value.Y + 1.1f,
                    point2.Value.Z + _random.Float(0.4f, 0.6f));
                var entity = SpawnCreature(templateName, position, creatureType.ConstantSpawn);
                if (entity != null)
                {
                    list.Add(entity);
                    count--;
                }
            }

            num++;
        }

        return list;
    }

    public Entity? SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
    {
        try
        {
            var entity = DatabaseManager.CreateEntity(Project, templateName, true)!;
            var componentBody = entity.FindComponent<ComponentBody>(true)!;
            componentBody.Position = position;
            componentBody.Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, _random.Float(0f, (float)Math.PI * 2f));
            var componentCreature = entity.FindComponent<ComponentCreature>();
            componentCreature?.ConstantSpawn = constantSpawn;
            Project.AddEntity(entity);
            return entity;
        }
        catch (Exception ex)
        {
            Log.Error($"Unable to spawn creature with template \"{templateName}\". Reason: {ex.Message}");
            return null;
        }
    }

    private Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
    {
        for (var i = 0; i < 5; i++)
        {
            var x = 16 * chunk.Point.X + _random.Int(0, 15);
            var y = _random.Int(10, 246);
            var z = 16 * chunk.Point.Y + _random.Int(0, 15);
            var result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
            if (result.HasValue)
            {
                return result;
            }
        }

        return null;
    }

    private Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
    {
        var x = spawnPoint.X;
        var num = MathUtils.Clamp(spawnPoint.Y, 1, 253);
        var z = spawnPoint.Z;
        var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell is not { State: > TerrainChunkState.InvalidPropagatedLight })
        {
            return null;
        }

        for (var i = 0; i < 30; i++)
        {
            var point = new Point3(x, num + i, z);
            if (TestSpawnPoint(point, spawnLocationType))
            {
                return point;
            }

            var point2 = new Point3(x, num - i, z);
            if (TestSpawnPoint(point2, spawnLocationType))
            {
                return point2;
            }
        }

        return null;
    }

    private bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
    {
        var x = spawnPoint.X;
        var y = spawnPoint.Y;
        var z = spawnPoint.Z;
        if (y is <= 3 or >= 254)
        {
            return false;
        }

        switch (spawnLocationType)
        {
            case SpawnLocationType.Surface:
            {
                var cellLightFast2 = _subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
                if (_subsystemSky.SkyLightValue - cellLightFast2 > 3)
                {
                    return false;
                }

                var cellContentsFast7 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
                var cellContentsFast8 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
                var cellContentsFast9 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
                var block6 = BlocksManager.Blocks[cellContentsFast7];
                var block7 = BlocksManager.Blocks[cellContentsFast8];
                var block8 = BlocksManager.Blocks[cellContentsFast9];
                if ((block6.Collidable || block6 is WaterBlock) && !block7.Collidable && block7 is not WaterBlock &&
                    !block8.Collidable)
                {
                    return block8 is not WaterBlock;
                }

                return false;
            }
            case SpawnLocationType.Cave:
            {
                var cellLightFast = _subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
                if (_subsystemSky.SkyLightValue - cellLightFast < 5)
                {
                    return false;
                }

                var cellContentsFast4 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
                var cellContentsFast5 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
                var cellContentsFast6 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
                var block3 = BlocksManager.Blocks[cellContentsFast4];
                var block4 = BlocksManager.Blocks[cellContentsFast5];
                var block5 = BlocksManager.Blocks[cellContentsFast6];
                if ((block3.Collidable || block3 is WaterBlock) && !block4.Collidable && block4 is not WaterBlock &&
                    !block5.Collidable)
                {
                    return block5 is not WaterBlock;
                }

                return false;
            }
            case SpawnLocationType.Water:
            {
                var cellContentsFast = _subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
                var cellContentsFast2 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
                var cellContentsFast3 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 2, z);
                var obj = BlocksManager.Blocks[cellContentsFast];
                var block = BlocksManager.Blocks[cellContentsFast2];
                var block2 = BlocksManager.Blocks[cellContentsFast3];
                if (obj is WaterBlock && !block.Collidable)
                {
                    return !block2.Collidable;
                }

                return false;
            }
            default:
                throw new InvalidOperationException("Unknown spawn location type.");
        }
    }

    private int CountCreatures(bool constantSpawn)
    {
        var num = 0;
        foreach (var body in _subsystemBodies.Bodies)
        {
            var componentCreature = body.Entity.FindComponent<ComponentCreature>();
            if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
            {
                num++;
            }
        }

        return num;
    }

    private int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
    {
        var num = 0;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesInArea(c1, c2, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentBody = _componentBodies.Array[i];
            var componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
            if (componentCreature == null || componentCreature.ConstantSpawn != constantSpawn)
            {
                continue;
            }

            var position = componentBody.Position;
            if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y)
            {
                num++;
            }
        }

        var point = Terrain.ToChunk(c1);
        var point2 = Terrain.ToChunk(c2);
        for (var j = point.X; j <= point2.X; j++)
        for (var k = point.Y; k <= point2.Y; k++)
        {
            var spawnChunk = _subsystemSpawn.GetSpawnChunk(new Point2(j, k));
            if (spawnChunk == null)
            {
                continue;
            }

            foreach (var spawnsDatum in spawnChunk.SpawnsData)
            {
                if (spawnsDatum.ConstantSpawn == constantSpawn)
                {
                    var position2 = spawnsDatum.Position;
                    if (position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y &&
                        position2.Z <= c2.Y)
                    {
                        num++;
                    }
                }
            }
        }

        return num;
    }

    private int GetRandomWeightedItem(IEnumerable<float> items)
    {
        var enumerable = items as float[] ?? items.ToArray();
        var max = MathUtils.Max(enumerable.Sum(), 1f);
        var num = _random.Float(0f, max);
        var num2 = 0;
        foreach (var item in enumerable)
        {
            if (num < item)
            {
                return num2;
            }

            num -= item;
            num2++;
        }

        return -1;
    }

    private SpawnLocationType GetRandomSpawnLocationType()
    {
        return _spawnLocations[_random.Int(0, _spawnLocations.Length - 1)];
    }

    private int GetSpawnFactorByPlayerCount()
    {
        var count = _subsystemPlayers.ComponentPlayers.Count;
        if (count > 10)
        {
            count = 10;
        }

        return count;
    }

    public class CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
    {
        public readonly bool ConstantSpawn = constantSpawn;

        public readonly string Name = name;

        public readonly bool RandomSpawn = randomSpawn;

        public required Func<CreatureType, Point3, int> SpawnFunction;

        public readonly SpawnLocationType SpawnLocationType = spawnLocationType;

        public required Func<CreatureType, Point3, float> SpawnSuitabilityFunction;

        public override string ToString()
        {
            return Name;
        }
    }
}
