using System.Globalization;
using System.Text;

using Engine.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemSpawn : Subsystem, IUpdateable
{
    public const float MaxChunkAge = 76800f;

    public const float VisitedRadius = 8f;

    public const float SpawnRadius = 40f;

    public const float DespawnRadius = 52f;

    private readonly Dictionary<Point2, SpawnChunk> _chunks = new();

    private double _nextChunkSpawnTime = 1.0;

    private double _nextDespawnTime = 1.0;

    private double _nextDiscardOldChunksTime = 1.0;

    private double _nextVisitedTime = 1.0;

    private readonly Random _random = new();

    private readonly Dictionary<ComponentSpawn, bool> _spawns = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public Dictionary<ComponentSpawn, bool>.KeyCollection Spawns => _spawns.Keys;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemTime.GameTime >= _nextDiscardOldChunksTime)
        {
            _nextDiscardOldChunksTime = _subsystemTime.GameTime + 60.0;
            DiscardOldChunks();
        }

        if (_subsystemTime.GameTime >= _nextVisitedTime)
        {
            _nextVisitedTime = _subsystemTime.GameTime + 5.0;
            UpdateLastVisitedTime();
        }

        if (_subsystemTime.GameTime >= _nextChunkSpawnTime)
        {
            _nextChunkSpawnTime = _subsystemTime.GameTime + 4.0;
            SpawnChunks();
        }

        if (_subsystemTime.GameTime >= _nextDespawnTime)
        {
            _nextDespawnTime = _subsystemTime.GameTime + 2.0;
            DespawnChunks();
        }
    }

    public event Action<SpawnChunk>? SpawningChunk;

    public SpawnChunk? GetSpawnChunk(Point2 point)
    {
        _chunks.TryGetValue(point, out var value);
        return value;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Chunks"))
        {
            var valuesDictionary2 = (ValuesDictionary)item.Value;
            var spawnChunk = new SpawnChunk
            {
                Point = HumanReadableConverter.ConvertFromString<Point2>(item.Key),
                IsSpawned = valuesDictionary2.GetValue<bool>("IsSpawned"),
                LastVisitedTime = valuesDictionary2.GetValue<double>("LastVisitedTime")
            };
            try
            {
                var value = valuesDictionary2.GetValue("SpawnsData", string.Empty);
                if (!string.IsNullOrEmpty(value))
                {
                    LoadSpawnsData(value, spawnChunk.SpawnsData);
                }

                _chunks[spawnChunk.Point] = spawnChunk;
            }
            catch (Exception e)
            {
                Log.Warning("SubsystemSpawn:" + e.Message);
            }
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Chunks", valuesDictionary2);
        foreach (var value2 in _chunks.Values)
        {
            if (!value2.LastVisitedTime.HasValue)
            {
                continue;
            }

            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(HumanReadableConverter.ConvertToString(value2.Point), valuesDictionary3);
            valuesDictionary3.SetValue("IsSpawned", value2.IsSpawned);
            valuesDictionary3.SetValue("LastVisitedTime", value2.LastVisitedTime.Value);
            var value = SaveSpawnsData(value2.SpawnsData);
            if (!string.IsNullOrEmpty(value))
            {
                valuesDictionary3.SetValue("SpawnsData", value);
            }
        }
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentSpawn>().OfType<ComponentSpawn>())
        {
            _spawns.Add(item, true);
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentSpawn>().OfType<ComponentSpawn>())
        {
            _spawns.Remove(item);
        }
    }

    private SpawnChunk GetOrCreateSpawnChunk(Point2 point)
    {
        var spawnChunk = GetSpawnChunk(point);
        if (spawnChunk != null)
        {
            return spawnChunk;
        }

        spawnChunk = new SpawnChunk
        {
            Point = point
        };
        _chunks.Add(point, spawnChunk);

        return spawnChunk;
    }

    private void DiscardOldChunks()
    {
        var list = new List<Point2>();
        foreach (var value in _chunks.Values)
        {
            if (!value.LastVisitedTime.HasValue ||
                _subsystemGameInfo.TotalElapsedGameTime - value.LastVisitedTime.Value > 76800.0)
            {
                list.Add(value.Point);
            }
        }

        foreach (var item in list)
        {
            _chunks.Remove(item);
        }
    }

    private void UpdateLastVisitedTime()
    {
        foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
        {
            var v = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
            var p = v - new Vector2(8f);
            var p2 = v + new Vector2(8f);
            var point = Terrain.ToChunk(p);
            var point2 = Terrain.ToChunk(p2);
            for (var i = point.X; i <= point2.X; i++)
            {
                for (var j = point.Y; j <= point2.Y; j++)
                {
                    var spawnChunk = GetSpawnChunk(new Point2(i, j));
                    spawnChunk?.LastVisitedTime = _subsystemGameInfo.TotalElapsedGameTime;
                }
            }
        }
    }

    private void SpawnChunks()
    {
        var centers = new List<Vector2>();
        foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
        {
            centers.Add(new Vector2(componentPlayer.ComponentBody.Position.X,
                componentPlayer.ComponentBody.Position.Z));
        }

        foreach (var v in centers)
        {
            var p = v - new Vector2(40f);
            var p2 = v + new Vector2(40f);
            var point = Terrain.ToChunk(p);
            var point2 = Terrain.ToChunk(p2);
            for (var i = point.X; i <= point2.X; i++)
            {
                for (var j = point.Y; j <= point2.Y; j++)
                {
                    var v2 = new Vector2((i + 0.5f) * 16f, (j + 0.5f) * 16f);
                    if (!(Vector2.DistanceSquared(v, v2) < 1600f))
                    {
                        continue;
                    }

                    var chunkAtCell =
                        _subsystemTerrain.Terrain.GetChunkAtCell(Terrain.ToCell(v2.X), Terrain.ToCell(v2.Y), false);
                    if (chunkAtCell is not { MainThreadState: > TerrainChunkState.InvalidPropagatedLight })
                    {
                        continue;
                    }

                    var point3 = new Point2(i, j);
                    var orCreateSpawnChunk = GetOrCreateSpawnChunk(point3);
                    foreach (var spawnsDatum in orCreateSpawnChunk.SpawnsData)
                    {
                        SpawnEntity(spawnsDatum);
                    }

                    orCreateSpawnChunk.SpawnsData.Clear();
                    SpawningChunk?.Invoke(orCreateSpawnChunk);
                    orCreateSpawnChunk.IsSpawned = true;
                }
            }
        }
    }

    private void DespawnChunks()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var list = new List<ComponentSpawn>();
        foreach (var key in _spawns.Keys)
        {
            if (key is not { AutoDespawn: true, IsDespawning: false })
            {
                continue;
            }

            var flag = true;
            var position = key.ComponentFrame.Position;
            var v = new Vector2(position.X, position.Z);
            foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
            {
                var viewPosition = componentPlayer.ComponentBody.Position;
                var v2 = new Vector2(viewPosition.X, viewPosition.Z);
                if (!(Vector2.DistanceSquared(v, v2) <= 2704f))
                {
                    continue;
                }

                flag = false;
                break;
            }

            if (flag)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            var point = Terrain.ToChunk(item.ComponentFrame.Position.XZ);
            GetOrCreateSpawnChunk(point).SpawnsData.Add(new SpawnEntityData
            {
                TemplateName = item.Entity.ValuesDictionary.DatabaseObject.Name,
                Position = item.ComponentFrame.Position,
                ConstantSpawn = item.ComponentCreature?.ConstantSpawn ?? false
            });
            item.Despawn();
        }
    }


    private void SpawnEntity(SpawnEntityData data)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        try
        {
            var entity = DatabaseManager.CreateEntity(Project, data.TemplateName, true)!;
            var componentBody = entity.FindComponent<ComponentBody>(true)!;
            componentBody.Position = data.Position;
            componentBody.Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, _random.Float(0f, (float)Math.PI * 2f));
            var componentCreature = entity.FindComponent<ComponentCreature>();
            componentCreature?.ConstantSpawn = data.ConstantSpawn;
            Project.AddEntity(entity);
        }
        catch (Exception ex)
        {
            Log.Error($"未能生成实体 \"{data.TemplateName}\". 原因: {ex.Message}");
        }
    }

    private void LoadSpawnsData(string data, List<SpawnEntityData> creaturesData)
    {
        var array = data.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        var num = 0;
        while (true)
        {
            if (num >= array.Length)
            {
                return;
            }

            var array2 = array[num].Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (array2.Length < 4)
            {
                break;
            }

            var spawnEntityData = new SpawnEntityData
            {
                TemplateName = array2[0],
                Position = new Vector3
                {
                    X = float.Parse(array2[1], CultureInfo.InvariantCulture),
                    Y = float.Parse(array2[2], CultureInfo.InvariantCulture),
                    Z = float.Parse(array2[3], CultureInfo.InvariantCulture)
                }
            };
            if (array2.Length >= 5)
            {
                spawnEntityData.ConstantSpawn = bool.Parse(array2[4]);
            }

            creaturesData.Add(spawnEntityData);
            num++;
        }

        throw new InvalidOperationException("Invalid spawn data string.");
    }

    private string SaveSpawnsData(List<SpawnEntityData> spawnsData)
    {
        var stringBuilder = new StringBuilder();
        foreach (var spawnsDatum in spawnsData)
        {
            stringBuilder.Append(spawnsDatum.TemplateName);
            stringBuilder.Append(',');
            stringBuilder.Append(
                (MathUtils.Round(spawnsDatum.Position.X * 10f) / 10f).ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(
                (MathUtils.Round(spawnsDatum.Position.Y * 10f) / 10f).ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(
                (MathUtils.Round(spawnsDatum.Position.Z * 10f) / 10f).ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(spawnsDatum.ConstantSpawn.ToString());
            stringBuilder.Append(';');
        }

        return stringBuilder.ToString();
    }
}
