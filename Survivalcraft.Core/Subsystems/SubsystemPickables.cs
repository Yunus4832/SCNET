using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemPickables : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private const int _outOfRangeRemoveTicks = 3;


    private readonly List<Pickable> _pickables = [];

    private readonly Dictionary<ushort, Pickable> _pickablesById = new();

    private readonly Queue<ushort> _availablePickableIds = new(ushort.MaxValue - 1);

    private readonly bool[] _usedPickableIds = new bool[ushort.MaxValue];

    public readonly List<Pickable> PickablesToRemove = [];


    private readonly Random _random = new();


    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemFluidBlockBehavior _subsystemFluidBlockBehavior = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;


    private SubsystemNetworkInterest _subsystemNetworkInterest = null!;

    private SubsystemCharacters _subsystemCharacters = null!;


    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly List<ComponentCharacter> _tmpPlayers = [];

    public ReadOnlyList<Pickable> Pickables => new(_pickables);


    public SubsystemPickables()
    {
        for (var id = 1; id < ushort.MaxValue; id++)
        {
            _availablePickableIds.Enqueue((ushort)id);
        }
    }


    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var totalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        var num = MathUtils.Pow(0.5f, dt);
        var num2 = MathUtils.Pow(0.001f, dt);
        _tmpPlayers.Clear();
        foreach (var componentCharacter in _subsystemCharacters.ComponentCharacters)
        {
            if (componentCharacter.ComponentHealth.Health > 0f)
            {
                _tmpPlayers.Add(componentCharacter);
            }
        }

        foreach (var pickable in _pickables)
        {
            if (!WorldRuntimeContext.For(Project).IsAuthority)
            {
                UpdateClientPosition(pickable, dt);
                _subsystemFluidBlockBehavior.GetSurfaceHeight(
                    Terrain.ToCell(pickable.Position.X),
                    Terrain.ToCell(pickable.Position.Y + 0.1f),
                    Terrain.ToCell(pickable.Position.Z),
                    out var clientSurface
                );
                UpdateFluidContact(pickable, clientSurface);
                continue;
            }

            if (pickable.ToRemove)
            {
                PickablesToRemove.Add(pickable);
                continue;
            }

            var block = WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(pickable.Value)];
            var num3 = _pickables.Count - PickablesToRemove.Count;
            var num4 = MathUtils.Lerp(300f, 90f, MathUtils.Saturate(num3 / 60f));
            var num5 = totalElapsedGameTime - pickable.CreationTime;
            if (num5 > num4)
            {
                pickable.ToRemove = true;
            }
            else
            {
                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(
                    Terrain.ToCell(pickable.Position.X),
                    Terrain.ToCell(pickable.Position.Z),
                    false
                );
                if (chunkAtCell is not { HasContents: true })
                {
                    continue;
                }

                var position = pickable.Position;
                var vector = position + pickable.Velocity * dt;
                if (!pickable.FlyToPosition.HasValue && num5 > 0.5)
                {
                    foreach (var tmpPlayer in _tmpPlayers)
                    {
                        var componentBody = tmpPlayer.ComponentBody;
                        var v = componentBody.Position + new Vector3(0f, 0.75f, 0f);
                        var num6 = (v - pickable.Position).LengthSquared();
                        if (!(num6 < 3.0625f))
                        {
                            continue;
                        }

                        //玩家获取到Pickable
                        pickable.GetPickPlayer = tmpPlayer.CharacterData.ClientId;
                        pickable.PositionFix = v;
                        pickable.Distance = num6;
                        if (WorldRuntimeContext.For(Project).IsAuthority)
                        {
                            OnPlayerGetPickable(pickable, tmpPlayer, v, num6);
                        }
                    }
                }

                if (pickable.FlyToPosition.HasValue)
                {
                    var v2 = pickable.FlyToPosition.Value - pickable.Position;
                    var num7 = v2.LengthSquared();
                    if (num7 >= 0.25f)
                    {
                        pickable.Velocity = 6f * v2 / MathUtils.Sqrt(num7);
                    }
                    else
                    {
                        pickable.FlyToPosition = null;
                    }
                }
                else
                {
                    var vector2 = _subsystemFluidBlockBehavior.CalculateFlowSpeed(
                        Terrain.ToCell(pickable.Position.X),
                        Terrain.ToCell(pickable.Position.Y + 0.1f),
                        Terrain.ToCell(pickable.Position.Z),
                        out var surfaceBlock,
                        out var surfaceHeight
                    );
                    if (!pickable.StuckMatrix.HasValue)
                    {
                        var terrainRaycastResult = _subsystemTerrain.Raycast(
                            position,
                            vector,
                            false,
                            true,
                            (value, _) => WorldContentBinding.For(Project).Catalog
                                .Blocks[Terrain.ExtractContents(value)].Collidable
                        );
                        if (terrainRaycastResult.HasValue)
                        {
                            var contents = Terrain.ExtractContents(
                                _subsystemTerrain.Terrain.GetCellValue(
                                    terrainRaycastResult.Value.CellFace.X,
                                    terrainRaycastResult.Value.CellFace.Y,
                                    terrainRaycastResult.Value.CellFace.Z
                                )
                            );
                            var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(contents);
                            foreach (var behavior in blockBehaviors)
                            {
                                behavior.OnHitByProjectile(terrainRaycastResult.Value.CellFace, pickable);
                            }

                            if (_subsystemTerrain.Raycast(
                                    position,
                                    position,
                                    false,
                                    true,
                                    (value2, _) =>
                                        WorldContentBinding.For(Project).Catalog
                                            .Blocks[Terrain.ExtractContents(value2)].Collidable
                                )
                                .HasValue)
                            {
                                var num8 = Terrain.ToCell(position.X);
                                var num9 = Terrain.ToCell(position.Y);
                                var num10 = Terrain.ToCell(position.Z);
                                var num11 = 0;
                                var num12 = 0;
                                var num13 = 0;
                                int? num14 = null;
                                for (var j = -3; j <= 3; j++)
                                {
                                    for (var k = -3; k <= 3; k++)
                                    {
                                        for (var l = -3; l <= 3; l++)
                                        {
                                            if (WorldContentBinding.For(Project).Catalog.Blocks[
                                                    _subsystemTerrain.Terrain.GetCellContents(
                                                        j + num8,
                                                        k + num9,
                                                        l + num10
                                                    )]
                                                .Collidable)
                                            {
                                                continue;
                                            }

                                            var num15 = j * j + k * k + l * l;
                                            if (num15 >= num14)
                                            {
                                                continue;
                                            }

                                            num11 = j + num8;
                                            num12 = k + num9;
                                            num13 = l + num10;
                                            num14 = num15;
                                        }
                                    }
                                }

                                if (num14.HasValue)
                                {
                                    pickable.FlyToPosition = new Vector3(num11, num12, num13) + new Vector3(0.5f);
                                }
                                else
                                {
                                    pickable.ToRemove = true;
                                }
                            }
                            else
                            {
                                var plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
                                var flag2 = vector2.HasValue && vector2.Value != Vector2.Zero;
                                if (plane.Normal.X != 0f)
                                {
                                    var num16 = flag2 ||
                                                MathUtils.Sqrt(
                                                    MathUtils.Sqr(pickable.Velocity.Y) +
                                                    MathUtils.Sqr(pickable.Velocity.Z)
                                                ) >
                                                10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(0f - num16, num16, num16);
                                }

                                if (plane.Normal.Y != 0f)
                                {
                                    var num17 = flag2 ||
                                                MathUtils.Sqrt(
                                                    MathUtils.Sqr(pickable.Velocity.X) +
                                                    MathUtils.Sqr(pickable.Velocity.Z)
                                                ) >
                                                10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(num17, 0f - num17, num17);
                                    if (flag2)
                                    {
                                        pickable.Velocity.Y += 0.1f * plane.Normal.Y;
                                    }
                                }

                                if (plane.Normal.Z != 0f)
                                {
                                    var num18 = flag2 ||
                                                MathUtils.Sqrt(
                                                    MathUtils.Sqr(pickable.Velocity.X) +
                                                    MathUtils.Sqr(pickable.Velocity.Y)
                                                ) >
                                                10f
                                        ? 0.95f
                                        : 0.25f;
                                    pickable.Velocity *= new Vector3(num18, num18, 0f - num18);
                                }

                                vector = position;
                            }
                        }
                    }
                    else
                    {
                        var vector3 = pickable.StuckMatrix.Value.Translation +
                                      pickable.StuckMatrix.Value.Up * block.ProjectileTipOffset;
                        if (!_subsystemTerrain.Raycast(
                                vector3,
                                vector3,
                                false,
                                true,
                                (value, _) =>
                                    WorldContentBinding.For(Project).Catalog
                                        .Blocks[Terrain.ExtractContents(value)].Collidable
                            ).HasValue)
                        {
                            pickable.Position = pickable.StuckMatrix.Value.Translation;
                            pickable.Velocity = Vector3.Zero;
                            pickable.StuckMatrix = null;
                        }
                    }

                    if (UpdateFluidContact(pickable, surfaceBlock) && surfaceBlock is MagmaBlock)
                    {
                        pickable.ToRemove = true;
                        _subsystemExplosions.TryExplodeBlock(
                            Terrain.ToCell(pickable.Position.X),
                            Terrain.ToCell(pickable.Position.Y),
                            Terrain.ToCell(pickable.Position.Z),
                            pickable.Value
                        );
                    }

                    if (_subsystemTime.PeriodicGameTimeEvent(1.0, pickable.GetHashCode() % 100 / 100.0) &&
                        (_subsystemTerrain.Terrain.GetCellContents(
                             Terrain.ToCell(pickable.Position.X),
                             Terrain.ToCell(pickable.Position.Y + 0.1f),
                             Terrain.ToCell(pickable.Position.Z)
                         ) ==
                         104 ||
                         _subsystemFireBlockBehavior.IsCellOnFire(
                             Terrain.ToCell(pickable.Position.X),
                             Terrain.ToCell(pickable.Position.Y + 0.1f),
                             Terrain.ToCell(pickable.Position.Z)
                         )))
                    {
                        RequestEffect(PickableEffectKind.Burn, pickable.Position, _random.Float(-0.2f, 0.2f));
                        pickable.ToRemove = true;
                        _subsystemExplosions.TryExplodeBlock(
                            Terrain.ToCell(pickable.Position.X),
                            Terrain.ToCell(pickable.Position.Y),
                            Terrain.ToCell(pickable.Position.Z),
                            pickable.Value
                        );
                    }

                    if (!pickable.StuckMatrix.HasValue)
                    {
                        if (vector2.HasValue && surfaceHeight.HasValue)
                        {
                            var num19 = surfaceHeight.Value - pickable.Position.Y;
                            var num20 = MathUtils.Saturate(3f * num19);
                            pickable.Velocity.X += 4f * dt * (vector2.Value.X - pickable.Velocity.X);
                            pickable.Velocity.Y -= 10f * dt;
                            pickable.Velocity.Y += 10f * (1f / block.Density * num20) * dt;
                            pickable.Velocity.Z += 4f * dt * (vector2.Value.Y - pickable.Velocity.Z);
                            pickable.Velocity.Y *= num2;
                        }
                        else
                        {
                            pickable.Velocity.Y -= 10f * dt;
                            pickable.Velocity *= num;
                        }
                    }
                }

                pickable.Position = vector;
            }
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        foreach (var item in PickablesToRemove)
        {
            var observers = _subsystemNetworkInterest.Entities
                .GetObservers(EntityInterestGroup.Pickables, item.Id)
                .ToArray();
            if (RemovePickable(item))
            {
                if (observers.Length > 0)
                {
                    NetworkSender.Send(
                        Project,
                        PickablePackageBuilder.CaptureRemoval(item),
                        PackageAudience.To(observers)
                    );
                }

                _subsystemNetworkInterest.Entities.RemoveEntity(EntityInterestGroup.Pickables, item.Id);
            }
        }

        PickablesToRemove.Clear();
        if (Time.PeriodicEvent(0.1, 0.0))
        {
            UpdateNetworkInterest();
        }
    }

    public event Action<Pickable>? PickableAdded;

    public event Action<Pickable>? PickableRemoved;

    public Pickable? AddPickable(int value, int count, Vector3 position, Vector3? velocity, Matrix? stuckMatrix)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return null;
        }

        var pickable = CreatePickable(null, value, count, position, velocity, stuckMatrix);
        if (pickable == null)
        {
            return null;
        }

        pickable.NetToRemove = true;
        return pickable;
    }

    public Pickable? CreatePickable(
        ushort? id,
        int value,
        int count,
        Vector3 position,
        Vector3? velocity,
        Matrix? stuckMatrix
    )
    {
        var pickableId = id ?? FindAvailableId();
        if (pickableId == 0 || id.HasValue && !TryReservePickableId(pickableId))
        {
            return null;
        }

        var pickable = new Pickable
        {
            Id = pickableId,
            Value = value,
            Count = count,
            Position = position,
            StuckMatrix = stuckMatrix,
            CreationTime = _subsystemGameInfo?.TotalElapsedGameTime ?? 0.0
        };

        if (velocity.HasValue)
        {
            pickable.Velocity = velocity.Value;
        }
        else if (Terrain.ExtractContents(value) == BuiltInBlockRegistry.GetIndex<ExperienceBlock>())
        {
            var vector = _random.Vector2(1.5f, 2f);
            pickable.Velocity = new Vector3(vector.X, 3f, vector.Y);
        }
        else
        {
            pickable.Velocity = new Vector3(
                _random.Float(-0.5f, 0.5f),
                _random.Float(1f, 1.2f),
                _random.Float(-0.5f, 0.5f)
            );
        }

        _pickables.Add(pickable);
        _pickablesById.Add(pickable.Id, pickable);
        PickableAdded?.Invoke(pickable);
        return pickable;
    }

    internal ushort FindAvailableId()
    {
        while (_availablePickableIds.TryDequeue(out var id))
        {
            if (_usedPickableIds[id])
            {
                continue;
            }

            _usedPickableIds[id] = true;
            return id;
        }

        return 0;
    }

    private bool TryReservePickableId(ushort id)
    {
        if (id == 0 || id == ushort.MaxValue || _usedPickableIds[id])
        {
            return false;
        }

        _usedPickableIds[id] = true;
        return true;
    }

    private void ReleasePickableId(ushort id)
    {
        if (id == 0 || id == ushort.MaxValue || !_usedPickableIds[id])
        {
            return;
        }

        _usedPickableIds[id] = false;
        _availablePickableIds.Enqueue(id);
    }

    private void OnPlayerGetPickable(
        Pickable pickable,
        ComponentCharacter tmpPlayer,
        Vector3 positionFix,
        float distance
    )
    {
        var flag = Terrain.ExtractContents(pickable.Value) == 248;
        var inventory = tmpPlayer.ComponentMiner.Inventory;
        if (!flag && ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value) < 0)
        {
            return;
        }

        if (distance < 1f)
        {
            if (flag)
            {
                tmpPlayer.ComponentLevel.AddExperience(pickable.Count, true);
                pickable.ToRemove = true;
            }
            else
            {
                pickable.Count = ComponentInventoryBase.AcquireItems(inventory, pickable.Value, pickable.Count);
                if (pickable.Count != 0 && WorldRuntimeContext.For(Project).IsAuthority)
                {
                    return;
                }

                pickable.ToRemove = true;
                pickable.PlaySound = true;
                NotifyPickableCollected(pickable);
            }
        }
        else if (!pickable.StuckMatrix.HasValue)
        {
            pickable.FlyToPosition =
                positionFix + 0.1f * MathUtils.Sqrt(distance) * tmpPlayer.ComponentBody.Velocity;
        }
    }

    public event Action<PickableEffect>? EffectRequested;

    public void NotifyPickableCollected(Pickable pickable)
    {
        RequestEffect(PickableEffectKind.Collected, pickable.Position, -0.4f);
    }

    private void RequestEffect(PickableEffectKind kind, Vector3 position, float pitch)
    {
        EffectRequested?.Invoke(new PickableEffect(kind, position, pitch));
    }

    public bool PickableAction(ushort id, Action<Pickable> action, bool requestSync = true)
    {
        var found = _pickablesById.TryGetValue(id, out var pickable);
        if (found)
        {
            action.Invoke(pickable!);
        }

        if (!found && !WorldRuntimeContext.For(Project).IsAuthority && requestSync)
        {
            NetworkSender.SendToServer(
                WorldRuntimeContext.For(Project),
                new PickableSyncRequestPackage
                    { Id = id, StateTick = unchecked((uint)Time.FrameIndex) }
            );
        }

        return found;
    }

    public bool TryGetPickable(ushort id, out Pickable pickable)
    {
        return _pickablesById.TryGetValue(id, out pickable!);
    }

    public void ApplyPositionSnapshot(IEnumerable<PickablePosition> positions, uint stateTick)
    {
        foreach (var position in positions)
        {
            PickableAction(
                position.Id,
                pickable =>
                {
                    ApplyMotionSnapshot(pickable, position, stateTick);
                }
            );
        }
    }

    internal static void ApplyMotionSnapshot(Pickable pickable, PickablePosition snapshot, uint stateTick)
    {
        if (unchecked((int)(stateTick - pickable.LastStateTick)) <= 0)
        {
            return;
        }

        pickable.LastStateTick = stateTick;
        pickable.NetworkPosition = snapshot.Position;
        pickable.Velocity = Vector3.Zero;
        pickable.FlyToPosition = null;
        pickable.ToRemove = false;
    }

    internal static void UpdateClientPosition(Pickable pickable, float dt)
    {
        if (pickable.NetworkPosition is { } position)
        {
            pickable.Position = Vector3.Lerp(pickable.Position, position, 1f - MathUtils.Exp(-20f * dt));
        }
    }

    private bool UpdateFluidContact(Pickable pickable, FluidBlock? surfaceBlock)
    {
        if (surfaceBlock is null)
        {
            pickable.SplashGenerated = false;
            return false;
        }

        if (pickable.SplashGenerated)
        {
            return false;
        }

        if (surfaceBlock is WaterBlock)
        {
            RequestEffect(PickableEffectKind.WaterEntry, pickable.Position, _random.Float(-0.2f, 0.2f));
        }
        else if (surfaceBlock is MagmaBlock)
        {
            RequestEffect(PickableEffectKind.MagmaEntry, pickable.Position, _random.Float(-0.2f, 0.2f));
        }

        pickable.SplashGenerated = true;
        return true;
    }

    public bool RemovePickable(Pickable pickable)
    {
        if (!_pickablesById.TryGetValue(pickable.Id, out var indexed) ||
            !ReferenceEquals(indexed, pickable) ||
            !_pickables.Remove(pickable))
        {
            return false;
        }

        _pickablesById.Remove(pickable.Id);
        ReleasePickableId(pickable.Id);
        PickableRemoved?.Invoke(pickable);
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemNetworkInterest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        _subsystemFluidBlockBehavior = Project.FindSubsystem<SubsystemFluidBlockBehavior>(true)!;
        foreach (ValuesDictionary item in valuesDictionary.GetValue<ValuesDictionary>("Pickables").Values
                     .Where(v => v is ValuesDictionary))
        {
            var pickable = new Pickable();
            pickable.Id = item.GetValue("Id", pickable.Id);
            pickable.Value = item.GetValue("Value", pickable.Value);
            pickable.Count = item.GetValue("Count", pickable.Count);
            pickable.Position = item.GetValue("Position", pickable.Position);
            pickable.Velocity = item.GetValue("Velocity", pickable.Velocity);
            pickable.CreationTime = item.GetValue("CreationTime", pickable.CreationTime);
            if (pickable.Id == 0 || !TryReservePickableId(pickable.Id))
            {
                pickable.Id = FindAvailableId();
            }

            if (pickable.Id == 0)
            {
                continue;
            }

            if (item.ContainsKey("StuckMatrix"))
            {
                pickable.StuckMatrix = item.GetValue("StuckMatrix", pickable.StuckMatrix);
            }

            _pickables.Add(pickable);
            _pickablesById.Add(pickable.Id, pickable);
        }
    }

    private void UpdateNetworkInterest()
    {
        var candidates = _subsystemNetworkInterest.GetPointCandidates(
            _pickables.Select(pickable =>
                ((int)pickable.Id, pickable.Position.XZ)
            )
        );
        foreach (var client in WorldConnections.For(Project).Members.Values)
        {
            if (client == WorldConnections.For(Project).Self || !client.IsConnected)
            {
                continue;
            }

            var current = candidates.TryGetValue(client, out var ids) ? ids : [];
            var relevant = current.Select(id => _pickablesById[(ushort)id]).ToList();
            var changes = _subsystemNetworkInterest.Entities.Synchronize(
                client,
                EntityInterestGroup.Pickables,
                current,
                id => _pickablesById.ContainsKey((ushort)id),
                _outOfRangeRemoveTicks
            );

            var entered = changes.Entered
                .Select(id => _pickablesById[(ushort)id])
                .ToList();
            if (entered.Count > 0)
            {
                NetworkSender.Send(
                    Project,
                    PickablePackageBuilder.CaptureAdded(entered),
                    PackageAudience.To(client)
                );
            }

            if (changes.Left.Count > 0)
            {
                var left = changes.Left.Select(id => new Pickable { Id = (ushort)id }).ToList();
                NetworkSender.Send(
                    Project,
                    PickablePackageBuilder.CaptureRemoved(left),
                    PackageAudience.To(client)
                );
            }

            // Bound each logical snapshot; the transport splits its actual encoding to the peer payload budget.
            for (var i = 0; i < relevant.Count; i += PickablePackage.MaxPositionsPerSnapshot)
            {
                NetworkSender.Send(
                    Project,
                    PickablePackageBuilder.Capture(
                        relevant.GetRange(
                            i,
                            Math.Min(PickablePackage.MaxPositionsPerSnapshot, relevant.Count - i)
                        )
                    ),
                    PackageAudience.To(client)
                );
            }
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Pickables", valuesDictionary2);
        if (Project.SendToClientMode)
        {
            return;
        }

        var num = 0;
        foreach (var pickable in _pickables)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(num.ToString(), valuesDictionary3);
            valuesDictionary3.SetValue("Id", pickable.Id);
            valuesDictionary3.SetValue("Value", pickable.Value);
            valuesDictionary3.SetValue("Count", pickable.Count);
            valuesDictionary3.SetValue("Position", pickable.Position);
            valuesDictionary3.SetValue("Velocity", pickable.Velocity);
            valuesDictionary3.SetValue("CreationTime", pickable.CreationTime);
            if (pickable.StuckMatrix.HasValue)
            {
                valuesDictionary3.SetValue("StuckMatrix", pickable.StuckMatrix.Value);
            }

            num++;
        }
    }
}
