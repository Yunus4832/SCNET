using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Spawning;

namespace Game.Subsystems;

public class SubsystemCreatureSpawn : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private static readonly SpawnLocationType[] _spawnLocations =
        EnumUtils.GetEnumValues(typeof(SpawnLocationType)).Cast<SpawnLocationType>().ToArray();

    private CreatureSpawnPolicy? _policy;

    private bool _loaded;

    public CreatureSpawnPolicy Policy =>
        _policy ?? throw new InvalidOperationException("Creature spawn policy is not configured.");

    public void ConfigurePolicy(CreatureSpawnPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (_loaded)
        {
            throw new InvalidOperationException("Creature spawn policy must be configured before loading the world.");
        }

        _policy = policy;
    }

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private readonly Dictionary<ComponentCreature, bool> _creatures = new();

    private readonly List<CreatureType> _creatureTypes = [];

    private readonly List<SpawnChunk> _newSpawnChunks = [];

    private readonly Random _random = new();

    private readonly List<SpawnChunk> _spawnChunks = [];

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemCharacters _subsystemCharacters = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemSpawn _subsystemSpawn = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public Dictionary<ComponentCreature, bool>.KeyCollection Creatures => _creatures.Keys;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != EnvironmentBehaviorMode.Living)
        {
            return;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(Policy.ConstantSpawnIntervalTime, 0))
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

        if (_subsystemTime.PeriodicGameTimeEvent(Policy.SpawnIntervalTime, 2.0))
        {
            SpawnRandomCreature();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _ = Policy;
        _loaded = true;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
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
        SubsystemSpatialSoundRequests? soundRequests
    )
    {
        if (string.IsNullOrEmpty(templateName))
        {
            throw new ArgumentException(nameof(templateName));
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
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
        soundRequests?.RequestSound("Audio/BlockPlaced", 1f, 0f, position, 3f, true);
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentCreature>().OfType<ComponentCreature>())
        {
            _creatures.Add(item, true);
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentCreature>().OfType<ComponentCreature>())
        {
            _creatures.Remove(item);
        }
    }

    private void InitializeCreatureTypes()
    {
        _creatureTypes.Add(
            new CreatureType("Duck", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                    return humidity > 8 &&
                           temperature > 4 &&
                           shoreDistance > 30f &&
                           point.Y >= topHeight &&
                           (WorldContentBinding.For(Project).Catalog.Blocks[groundContents] is LeavesBlock ||
                            groundContents == 18 ||
                            groundContents == 8 ||
                            groundContents == 2)
                        ? 2.5f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Duck", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Raven", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                    return (humidity <= 8 || temperature <= 4) &&
                           shoreDistance > 30f &&
                           point.Y >= topHeight &&
                           (WorldContentBinding.For(Project).Catalog.Blocks[groundContents] is LeavesBlock ||
                            groundContents == 62 ||
                            groundContents == 8 ||
                            groundContents == 2 ||
                            groundContents == 7)
                        ? 2.5f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Raven", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Seagull", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                    return shoreDistance is > -100f and < 40f &&
                           point.Y >= topHeight &&
                           groundContents is 18 or 7 or 6 or 62
                        ? 2.5f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Seagull", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Wildboar", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           humidity > 8 &&
                           point.Y < 80 &&
                           (groundContents == 8 || groundContents == 2)
                        ? 0.25f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Wildboar", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Brown Cattle", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           humidity > 4 &&
                           temperature >= 8 &&
                           point.Y < 70 &&
                           (groundContents == 8 || groundContents == 2)
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var herdSize = _random.Int(3, 5);
                    var bullCount = MathUtils.Min(_random.Int(1, 3), herdSize);
                    var cowCount = herdSize - bullCount;
                    return 0 +
                           SpawnCreatures(creatureType, "Bull_Brown", point, bullCount).Count +
                           SpawnCreatures(creatureType, "Cow_Brown", point, cowCount).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Black Cattle", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           humidity > 4 &&
                           temperature < 8 &&
                           point.Y < 70 &&
                           (groundContents == 8 || groundContents == 2)
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var herdSize = _random.Int(3, 5);
                    var bullCount = MathUtils.Min(_random.Int(1, 3), herdSize);
                    var cowCount = herdSize - bullCount;
                    return 0 +
                           SpawnCreatures(creatureType, "Bull_Black", point, bullCount).Count +
                           SpawnCreatures(creatureType, "Cow_Black", point, cowCount).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("White Bull", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           humidity > 8 &&
                           temperature < 4 &&
                           point.Y < 70 &&
                           (groundContents == 8 || groundContents == 2)
                        ? 0.01f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bull_White", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Gray Wolves", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           humidity >= 8 &&
                           point.Y < 100 &&
                           (groundContents == 8 || groundContents == 2)
                        ? 0.075f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Wolf_Gray", point, _random.Int(1, 3)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Coyotes", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature > 8 &&
                           humidity is < 8 and >= 2 &&
                           point.Y < 100 &&
                           groundContents == 7
                        ? 0.075f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Wolf_Coyote", point, _random.Int(1, 3)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Brown Bears", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           humidity >= 4 &&
                           temperature >= 8 &&
                           point.Y < 110 &&
                           groundContents is 8 or 2 or 3
                        ? 0.1f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Brown", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Black Bears", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           humidity >= 4 &&
                           temperature < 8 &&
                           point.Y < 120 &&
                           groundContents is 8 or 2 or 3
                        ? 0.1f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Polar Bears", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > -40f && temperature < 8 && point.Y < 80 && groundContents == 62 ? 0.1f : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Polar", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Horses", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 3 &&
                           humidity > 6 &&
                           point.Y < 80 &&
                           groundContents is 8 or 2 or 3
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var spawnedCount = 0;
                    if (_random.Float(0f, 1f) < 0.35f)
                    {
                        spawnedCount += SpawnCreatures(creatureType, "Horse_Black", point, 1).Count;
                    }

                    if (_random.Float(0f, 1f) < 0.5f)
                    {
                        spawnedCount += SpawnCreatures(creatureType, "Horse_Bay", point, 1).Count;
                    }

                    if (_random.Float(0f, 1f) < 0.5f)
                    {
                        spawnedCount += SpawnCreatures(creatureType, "Horse_Chestnut", point, 1).Count;
                    }

                    if (temperature > 8 && _random.Float(0f, 1f) < 0.3f)
                    {
                        spawnedCount += SpawnCreatures(creatureType, "Horse_Palomino", point, 1).Count;
                    }

                    if (temperature < 8 && _random.Float(0f, 1f) < 0.3f)
                    {
                        spawnedCount += SpawnCreatures(creatureType, "Horse_White", point, 1).Count;
                    }

                    return spawnedCount;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Camels", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f && temperature > 8 && humidity < 8 && point.Y < 80 && groundContents == 7
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Camel", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Donkeys", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 6 &&
                           point.Y < 120 &&
                           groundContents is 8 or 2 or 3 or 7
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Donkey", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Giraffes", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 8 &&
                           humidity > 7 &&
                           point.Y < 75 &&
                           groundContents is 8 or 2 or 3
                        ? 0.03f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Giraffe", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Rhinos", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature > 8 &&
                           humidity > 7 &&
                           point.Y < 75 &&
                           groundContents is 8 or 2 or 3
                        ? 0.04f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Rhino", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Tigers", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           humidity > 8 &&
                           point.Y < 80 &&
                           groundContents is 8 or 2 or 3 or 7
                        ? 0.025f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("White Tigers", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature < 2 &&
                           point.Y < 90 &&
                           groundContents is 8 or 2 or 3 or 7 or 62
                        ? 0.025f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger_White", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Lions", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature > 8 &&
                           point.Y < 80 &&
                           groundContents is 8 or 2 or 3 or 7
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Lion", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Jaguars", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           humidity > 8 &&
                           temperature > 8 &&
                           point.Y < 100 &&
                           groundContents is 8 or 2 or 3 or 7 or 12
                        ? 0.04f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Leopards", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature > 8 &&
                           point.Y < 120 &&
                           groundContents is 8 or 2 or 3 or 7 or 12
                        ? 0.04f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Zebras", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 8 &&
                           humidity > 7 &&
                           point.Y < 80 &&
                           groundContents is 8 or 2 or 3
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Zebra", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Gnus", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 8 &&
                           point.Y < 80 &&
                           (groundContents == 8 || groundContents == 2 || groundContents == 3)
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Gnu", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Reindeers", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return temperature < 3 &&
                           point.Y < 90 &&
                           (groundContents == 8 || groundContents == 2 || groundContents == 3 || groundContents == 62)
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Reindeer", point, _random.Int(1, 3)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Mooses", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return temperature < 7 &&
                           point.Y < 90 &&
                           (groundContents == 8 || groundContents == 2 || groundContents == 3 || groundContents == 62)
                        ? 0.1f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Moose", point, _random.Int(1, 1)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Bisons", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return temperature < 10 &&
                           humidity < 12 &&
                           point.Y < 80 &&
                           groundContents is 8 or 2 or 3 or 62
                        ? 0.1f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Bison", point, _random.Int(1, 4)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Ostriches", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 8 &&
                           humidity < 8 &&
                           point.Y < 75 &&
                           groundContents is 8 or 2 or 7
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Ostrich", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cassowaries", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 20f &&
                           temperature > 8 &&
                           humidity < 12 &&
                           point.Y < 75 &&
                           groundContents is 8 or 2 or 7
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Cassowary", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Hyenas", SpawnLocationType.Surface, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return shoreDistance > 40f &&
                           temperature > 8 &&
                           point.Y < 80 &&
                           (groundContents == 8 || groundContents == 2 || groundContents == 7)
                        ? 0.05f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Hyena", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Bears", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 ? 1f : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var templateName = _random.Int(0, 1) == 0 ? "Bear_Black" : "Bear_Brown";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Tigers", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 ? 0.25f : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Lions", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 &&
                           temperature > 8 &&
                           humidity < 8
                        ? 0.25f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Lion", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Jaguars", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 ? 0.5f : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Leopards", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 ? 0.25f : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Cave Hyenas", SpawnLocationType.Cave, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    return groundContents is 3 or 67 or 4 or 66 or 2 or 7 &&
                           temperature > 8
                        ? 1f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Hyena", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Bull Sharks", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                        ? 0f
                        : 0.4f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Shark_Bull";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Tiger Sharks", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < -5f)
                        ? 0f
                        : 0.3f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Shark_Tiger";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Great White Sharks", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < -20f)
                        ? 0f
                        : 0.2f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Shark_GreatWhite";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Barracudas", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                        ? 0f
                        : 0.5f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Barracuda";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Bass_Sea", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < -2f)
                        ? 0f
                        : 1f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Bass_Sea";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Bass_Freshwater", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    return shoreDistance > 10f && temperature >= 4 ? 1f : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Bass_Freshwater";
                    return SpawnCreatures(creatureType, templateName, point, _random.Int(1, 2)).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Rays", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = (_, point) =>
                    !(_subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z) < 10f)
                        ? 1f
                        : 0.5f,
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var dirtSamples = 0;
                    var sandSamples = 0;
                    for (var i = point.X - 2; i <= point.X + 2; i++)
                    {
                        for (var j = point.Z - 2; j <= point.Z + 2; j++)
                        {
                            if (_subsystemTerrain.Terrain.GetCellContents(point.X, point.Y, point.Z) == 18)
                            {
                                for (var sampleY = point.Y - 1; sampleY > 0; sampleY--)
                                {
                                    switch (_subsystemTerrain.Terrain.GetCellContents(point.X, sampleY, point.Z))
                                    {
                                        case 2:
                                            dirtSamples++;
                                            break;
                                        case 7:
                                            sandSamples++;
                                            break;
                                        default:
                                            continue;
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    var templateName = dirtSamples >= sandSamples ? "Ray_Brown" : "Ray_Yellow";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Piranhas", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    return shoreDistance > 10f && humidity >= 4 && temperature >= 7 ? 1f : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Piranha";
                    return SpawnCreatures(creatureType, templateName, point, _random.Int(2, 4)).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Orcas", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    if (shoreDistance < -100f)
                    {
                        return 0.05f;
                    }

                    return shoreDistance < -20f ? 0.01f : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Orca";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Belugas", SpawnLocationType.Water, false, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    if (shoreDistance < -100f)
                    {
                        return 0.05f;
                    }

                    return shoreDistance < -20f ? 0.01f : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    const string templateName = "Beluga";
                    return SpawnCreatures(creatureType, templateName, point, 1).Count;
                }
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Gray Wolves", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance =
                        _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    float temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (((shoreDistance > 20f && humidity >= 8) ||
                         (temperature <= 8f && point.Y < 90 && lightAbove <= 7)) &&
                        groundContents is 8 or 2)
                    {
                        return 2f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Wolf_Gray", point, _random.Int(1, 3)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Coyotes", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance =
                        _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    float humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    float temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature > 8f &&
                        humidity < 8f &&
                        point.Y < 90 &&
                        lightAbove <= 7 &&
                        groundContents == 7)
                    {
                        return 2f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Wolf_Coyote", point, _random.Int(1, 3)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Brown Bears", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance =
                        _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        humidity >= 4 &&
                        temperature >= 8 &&
                        point.Y < 100 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3)
                    {
                        return 0.5f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Brown", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Black Bears", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance =
                        _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature < 8 &&
                        point.Y < 110 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3)
                    {
                        return 0.5f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Polar Bears", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance =
                        _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > -40f &&
                        temperature < 8 &&
                        point.Y < 90 &&
                        lightAbove <= 7 &&
                        groundContents == 62)
                    {
                        return 0.25f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Bear_Black", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Tigers", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance = _subsystemTerrain.Environment
                        .CalculateOceanShoreDistance(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        humidity > 8 &&
                        point.Y < 90 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3)
                    {
                        return 0.05f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Tiger", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Lions", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance = _subsystemTerrain.Environment
                        .CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature > 8 &&
                        point.Y < 90 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3 or 7)
                    {
                        return 0.25f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Lion", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Jaguars", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance = _subsystemTerrain.Environment
                        .CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var humidity = _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature > 8 &&
                        humidity > 8 &&
                        point.Y < 100 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3 or 12)
                    {
                        return 0.25f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Jaguar", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Leopards", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance = _subsystemTerrain.Environment
                        .CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature > 8 &&
                        point.Y < 110 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3 or 12)
                    {
                        return 0.25f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Leopard", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Constant Hyenas", SpawnLocationType.Surface, false, true)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    if (!(_subsystemSky.SkyLightIntensity < 0.1f))
                    {
                        return 0f;
                    }

                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(point.X, point.Z);
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    if (!_subsystemTerrain.TryGetEffectiveLight(point.X, point.Y + 1, point.Z, out var lightAbove))
                    {
                        return 0f;
                    }

                    if (shoreDistance > 20f &&
                        temperature > 8 &&
                        point.Y < 100 &&
                        lightAbove <= 7 &&
                        groundContents is 8 or 2 or 3 or 7)
                    {
                        return 1f;
                    }

                    return 0f;
                },
                SpawnFunction = (creatureType, point) =>
                    SpawnCreatures(creatureType, "Hyena", point, _random.Int(1, 2)).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Pigeon", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                    return temperature > 3 &&
                           shoreDistance > 30f &&
                           point.Y >= topHeight &&
                           (WorldContentBinding.For(Project).Catalog.Blocks[groundContents] is LeavesBlock ||
                            groundContents == 8 ||
                            groundContents == 2 ||
                            groundContents == 7)
                        ? 1.5f
                        : 0f;
                },
                SpawnFunction = (creatureType, point) => SpawnCreatures(creatureType, "Pigeon", point, 1).Count
            }
        );
        _creatureTypes.Add(
            new CreatureType("Sparrow", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate(CreatureType _, Point3 point)
                {
                    var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(
                        point.X,
                        point.Z
                    );
                    var temperature = _subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
                    _subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
                    var groundContents = Terrain.ExtractContents(
                        _subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z)
                    );
                    var topHeight = _subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
                    return temperature > 3 &&
                           shoreDistance > 20f &&
                           point.Y >= topHeight &&
                           (WorldContentBinding.For(Project).Catalog.Blocks[groundContents] is LeavesBlock ||
                            groundContents == 8 ||
                            groundContents == 2 ||
                            groundContents == 7)
                        ? 2f
                        : 0f;
                },
                SpawnFunction = delegate(CreatureType creatureType, Point3 point)
                {
                    var count3 = _random.Int(1, 2);
                    return SpawnCreatures(creatureType, "Sparrow", point, count3).Count;
                }
            }
        );
    }

    private void SpawnRandomCreature()
    {
        if (GetSpawnFactorByPlayerCount() == 0)
        {
            return;
        }

        //判断全区块生物总数限制
        if (CountCreatures(false) >= Policy.TotalLimit)
        {
            return;
        }

        var centers = new List<Vector3>();
        foreach (var componentCharacter in _subsystemCharacters.ComponentCharacters)
        {
            centers.Add(componentCharacter.ComponentBody.Position);
        }

        foreach (var center in centers)
        {
            var v = new Vector2(center.X, center.Z);
            if (CountCreaturesInArea(v - new Vector2(60f), v + new Vector2(60f), false) >= Policy.MaxPlayerAreaLimit)
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
            if (CountCreaturesInArea(c2, c3, false) >= Policy.MaxPointLimit)
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
        var totalLimit = constantSpawn ? Policy.TotalLimitConstant : Policy.TotalLimit;
        var areaLimit = constantSpawn ? Policy.AreaLimitConstant : Policy.AreaLimit;
        float areaRadius = constantSpawn ? Policy.AreaRadiusConstant : Policy.AreaRadius;
        var totalCount = CountCreatures(constantSpawn);
        var areaMin = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(areaRadius);
        var areaMax = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(areaRadius);
        var areaCount = CountCreaturesInArea(areaMin, areaMax, constantSpawn);
        for (var i = 0; i < maxAttempts; i++)
        {
            if (totalCount >= totalLimit) //总数超出，不生成
            {
                break;
            }

            if (areaCount >= areaLimit) //区块超出，不生成
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
                c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
            );
            var creatureTypes = source as CreatureType[] ?? source.ToArray();
            var items = creatureTypes.Select(c => c.SpawnSuitabilityFunction(c, spawnPoint.Value));
            var randomWeightedItem = GetRandomWeightedItem(items);
            if (randomWeightedItem < 0)
            {
                continue;
            }

            var creatureType = creatureTypes.ElementAt(randomWeightedItem);
            var spawnedCount = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
            totalCount += spawnedCount;
            areaCount += spawnedCount;
        }
    }

    private List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
    {
        var spawnedEntities = new List<Entity>();
        var attempts = 0;
        while (count > 0 && attempts < 50)
        {
            var spawnPoint = point;
            if (attempts > 0)
            {
                spawnPoint.X += _random.Int(-8, 8);
                spawnPoint.Y += _random.Int(-4, 8);
                spawnPoint.Z += _random.Int(-8, 8);
            }

            var validSpawnPoint = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
            if (validSpawnPoint.HasValue &&
                creatureType.SpawnSuitabilityFunction(creatureType, validSpawnPoint.Value) > 0f)
            {
                var position = new Vector3(
                    validSpawnPoint.Value.X + _random.Float(0.4f, 0.6f),
                    validSpawnPoint.Value.Y + 1.1f,
                    validSpawnPoint.Value.Z + _random.Float(0.4f, 0.6f)
                );
                var entity = SpawnCreature(templateName, position, creatureType.ConstantSpawn);
                if (entity != null)
                {
                    spawnedEntities.Add(entity);
                    count--;
                }
            }

            attempts++;
        }

        return spawnedEntities;
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
        var startY = MathUtils.Clamp(spawnPoint.Y, 1, 253);
        var z = spawnPoint.Z;
        var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell is not { HasCurrentLight: true })
        {
            return null;
        }

        for (var offsetY = 0; offsetY < 30; offsetY++)
        {
            var upperCandidate = new Point3(x, startY + offsetY, z);
            if (TestSpawnPoint(upperCandidate, spawnLocationType))
            {
                return upperCandidate;
            }

            var lowerCandidate = new Point3(x, startY - offsetY, z);
            if (TestSpawnPoint(lowerCandidate, spawnLocationType))
            {
                return lowerCandidate;
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

        var environment = _subsystemTerrain.CaptureLightEnvironment();
        switch (spawnLocationType)
        {
            case SpawnLocationType.Surface:
            {
                if (!_subsystemTerrain.Terrain.Lighting.TryGetEffectiveLight(
                        x,
                        y,
                        z,
                        environment,
                        out var cellLightFast2
                    )) { return false; }

                if (environment.SkyLightValue - cellLightFast2 > 3)
                {
                    return false;
                }

                var cellContentsFast7 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
                var cellContentsFast8 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
                var cellContentsFast9 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
                var block6 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast7];
                var block7 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast8];
                var block8 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast9];
                if ((block6.Collidable || block6 is WaterBlock) &&
                    !block7.Collidable &&
                    block7 is not WaterBlock &&
                    !block8.Collidable)
                {
                    return block8 is not WaterBlock;
                }

                return false;
            }
            case SpawnLocationType.Cave:
            {
                if (!_subsystemTerrain.Terrain.Lighting.TryGetEffectiveLight(
                        x,
                        y,
                        z,
                        environment,
                        out var cellLightFast
                    )) { return false; }

                if (environment.SkyLightValue - cellLightFast < 5)
                {
                    return false;
                }

                var cellContentsFast4 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
                var cellContentsFast5 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
                var cellContentsFast6 = _subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
                var block3 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast4];
                var block4 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast5];
                var block5 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast6];
                if ((block3.Collidable || block3 is WaterBlock) &&
                    !block4.Collidable &&
                    block4 is not WaterBlock &&
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
                var obj = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast];
                var block = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast2];
                var block2 = WorldContentBinding.For(Project).Catalog.Blocks[cellContentsFast3];
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
        var count = 0;
        foreach (var body in _subsystemBodies.Bodies)
        {
            var componentCreature = body.Entity.FindComponent<ComponentCreature>();
            if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
            {
                count++;
            }
        }

        return count;
    }

    private int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
    {
        var count = 0;
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
                count++;
            }
        }

        var point = Terrain.ToChunk(c1);
        var point2 = Terrain.ToChunk(c2);
        for (var j = point.X; j <= point2.X; j++)
        {
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
                        if (position2.X >= c1.X &&
                            position2.X <= c2.X &&
                            position2.Z >= c1.Y &&
                            position2.Z <= c2.Y)
                        {
                            count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    private int GetRandomWeightedItem(IEnumerable<float> items)
    {
        var weights = items as float[] ?? items.ToArray();
        var weightRange = MathUtils.Max(weights.Sum(), 1f);
        var remainingWeight = _random.Float(0f, weightRange);
        var index = 0;
        foreach (var weight in weights)
        {
            if (remainingWeight < weight)
            {
                return index;
            }

            remainingWeight -= weight;
            index++;
        }

        return -1;
    }

    private SpawnLocationType GetRandomSpawnLocationType()
    {
        return _spawnLocations[_random.Int(0, _spawnLocations.Length - 1)];
    }

    private int GetSpawnFactorByPlayerCount()
    {
        var count = _subsystemCharacters.ComponentCharacters.Count;
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
