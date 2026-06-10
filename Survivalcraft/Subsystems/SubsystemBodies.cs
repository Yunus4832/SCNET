using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemBodies : Subsystem, IUpdateable
{
    public const float AreaSize = 8f;

    private readonly Dictionary<Client, List<ComponentBody>> _toSendList = new();

    private Dictionary<ushort, ComponentBody> _idBodies = new();

    private readonly Dictionary<ComponentBody, Point2> _areaByComponentBody = new();

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private readonly Dictionary<Point2, DynamicArray<ComponentBody>> _componentBodiesByArea = new();

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemUpdate _subsystemUpdate = null!;

    public Dictionary<ComponentBody, Point2>.KeyCollection Bodies => _areaByComponentBody.Keys;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            var flag = _subsystemUpdate.IsLastUpdateInFrame && Time.PeriodicEvent(0.1, 0.0);
            _toSendList.Clear();
            foreach (var body in Bodies)
            {
                UpdateBody(body);
                if (!flag)
                {
                    continue;
                }

                foreach (var playerData in _subsystemPlayers.PlayersData)
                {
                    if (playerData.IsMainPlayer)
                    {
                        continue;
                    }

                    if (playerData.Client == null || playerData.ComponentPlayer == null ||
                        !_subsystemTerrain.TerrainUpdater.UpdateLocations.ContainsKey(playerData.PlayerIndex) ||
                        !IsBodyInRange(body.Position.XZ,
                            _subsystemTerrain.TerrainUpdater.UpdateLocations[playerData.PlayerIndex]))
                    {
                        continue;
                    }

                    if (!_toSendList.TryGetValue(playerData.Client, out var list))
                    {
                        list = [];
                        _toSendList.Add(playerData.Client, list);
                    }

                    if (body.Player == null && body.ChildBodies.Count == 0 && flag)
                    {
                        list.Add(body);
                    }
                }
            }

            if (_toSendList.Count <= 0)
            {
                return;
            }

            {
                foreach (var item in _toSendList)
                {
                    if (item.Value.Count > 0)
                    {
                        CommonLib.Net.QueuePackage(new SubsystemBodyPackage(item.Value) { To = item.Key });
                    }
                }

                //在这里重置标志
                foreach (var item in _toSendList)
                foreach (var body in item.Value)
                {
                    body.SendPosition = null;
                    body.SendRotation = null;
                    body.SendVelocity = null;
                    if (body.Locomotion == null)
                    {
                        continue;
                    }

                    body.Locomotion.SendLookAngles = null;
                    body.Locomotion.FlyOrderChange = false;
                }
            }
        }
        else
        {
            foreach (var body in Bodies)
            {
                UpdateBody(body);
            }
        }
    }

    public void FindBodiesAroundPoint(Vector2 point, float radius, DynamicArray<ComponentBody> result)
    {
        var num = (int)MathUtils.Floor((point.X - radius) / 8f);
        var num2 = (int)MathUtils.Floor((point.Y - radius) / 8f);
        var num3 = (int)MathUtils.Floor((point.X + radius) / 8f);
        var num4 = (int)MathUtils.Floor((point.Y + radius) / 8f);
        for (var i = num; i <= num3; i++)
        for (var j = num2; j <= num4; j++)
        {
            if (_componentBodiesByArea.TryGetValue(new Point2(i, j), out var value))
            {
                for (var k = 0; k < value.Count; k++)
                {
                    result.Add(value.Array[k]);
                }
            }
        }
    }

    public void FindBodiesInArea(Vector2 corner1, Vector2 corner2, DynamicArray<ComponentBody> result)
    {
        var point = new Point2((int)MathUtils.Floor(corner1.X / 8f), (int)MathUtils.Floor(corner1.Y / 8f));
        var point2 = new Point2((int)MathUtils.Floor(corner2.X / 8f), (int)MathUtils.Floor(corner2.Y / 8f));
        var num = MathUtils.Min(point.X, point2.X) - 1;
        var num2 = MathUtils.Min(point.Y, point2.Y) - 1;
        var num3 = MathUtils.Max(point.X, point2.X) + 1;
        var num4 = MathUtils.Max(point.Y, point2.Y) + 1;
        for (var i = num; i <= num3; i++)
        for (var j = num2; j <= num4; j++)
        {
            if (_componentBodiesByArea.TryGetValue(new Point2(i, j), out var value))
            {
                for (var k = 0; k < value.Count; k++)
                {
                    result.Add(value.Array[k]);
                }
            }
        }
    }

    public BodyRaycastResult? Raycast(Vector3 start, Vector3 end, float inflateAmount,
        Func<ComponentBody, float, bool> action)
    {
        var num = Vector3.Distance(start, end);
        var ray = new Ray3(start, num > 0f ? (end - start) / num : Vector3.UnitX);
        var corner = new Vector2(start.X, start.Z);
        var corner2 = new Vector2(end.X, end.Z);
        var bodyRaycastResult = default(BodyRaycastResult);
        bodyRaycastResult.Ray = ray;
        bodyRaycastResult.Distance = float.MaxValue;
        var value = bodyRaycastResult;
        _componentBodies.Clear();
        FindBodiesInArea(corner, corner2, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentBody = _componentBodies.Array[i];
            float? num2;
            if (inflateAmount > 0f)
            {
                var boundingBox = componentBody.BoundingBox;
                boundingBox.Min -= new Vector3(inflateAmount);
                boundingBox.Max += new Vector3(inflateAmount);
                num2 = ray.Intersection(boundingBox);
            }
            else
            {
                num2 = ray.Intersection(componentBody.BoundingBox);
            }

            if (!(num2 <= num) || !(num2.Value < value.Distance) || !action(componentBody, num2.Value))
            {
                continue;
            }

            value.Distance = num2.Value;
            value.ComponentBody = componentBody;
        }

        if (value.ComponentBody == null)
        {
            return null;
        }

        return value;
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentBody>())
        {
            if (item != null)
            {
                AddBody(item);
            }
        }
    }

    public void FindBodyByCreatureID(int creatureId, Action<ComponentBody>? action = null, Action? fail = null)
    {
        if (creatureId <= 0)
        {
            fail?.Invoke();
            return;
        }

        if (_idBodies.TryGetValue((ushort)creatureId, out var body))
        {
            action?.Invoke(body);
            return;
        }

        foreach (var componentBody in Bodies)
        {
            if (componentBody.Entity.EntityId != creatureId)
            {
                continue;
            }

            _idBodies[(ushort)creatureId] = componentBody;
            action?.Invoke(componentBody);
            return;
        }

        fail?.Invoke();
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentBody>())
        {
            if (item != null)
            {
                RemoveBody(item);
            }
        }
    }

    private static bool IsBodyInRange(Vector2 position, TerrainUpdater.UpdateLocation location)
    {
        var distance = Vector2.DistanceSquared(location.Center, position);
        var content = MathUtils.Sqr(location.ContentDistance);
        return distance <= content;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
    }

    private void AddBody(ComponentBody componentBody)
    {
        var position = componentBody.Position;
        var point = new Point2((int)MathUtils.Floor(position.X / 8f), (int)MathUtils.Floor(position.Z / 8f));
        _areaByComponentBody.Add(componentBody, point);
        if (!_componentBodiesByArea.TryGetValue(point, out var value))
        {
            value = [];
            _componentBodiesByArea.Add(point, value);
        }

        value.Add(componentBody);
        if (componentBody.Entity.EntityId != 0)
        {
            _idBodies[(ushort)componentBody.Entity.EntityId] = componentBody;
        }

        componentBody.PositionChanged += ComponentBodyPositionChanged;
    }

    private void RemoveBody(ComponentBody componentBody)
    {
        if (_areaByComponentBody.Remove(componentBody, out var key))
        {
            _componentBodiesByArea[key].Remove(componentBody);
        }

        if (componentBody.Entity.EntityId != 0)
        {
            _idBodies.Remove((ushort)componentBody.Entity.EntityId);
        }

        componentBody.PositionChanged -= ComponentBodyPositionChanged;
    }

    private void UpdateBody(ComponentBody componentBody)
    {
        var position = componentBody.Position;
        var point = new Point2((int)MathUtils.Floor(position.X / 8f), (int)MathUtils.Floor(position.Z / 8f));
        var point2 = _areaByComponentBody[componentBody];
        if (point == point2)
        {
            return;
        }

        _areaByComponentBody[componentBody] = point;
        _componentBodiesByArea[point2].Remove(componentBody);
        if (!_componentBodiesByArea.TryGetValue(point, out var value))
        {
            value = [];
            _componentBodiesByArea.Add(point, value);
        }

        value.Add(componentBody);
    }

    private void ComponentBodyPositionChanged(ComponentFrame componentFrame)
    {
        UpdateBody((ComponentBody)componentFrame);
    }
}
