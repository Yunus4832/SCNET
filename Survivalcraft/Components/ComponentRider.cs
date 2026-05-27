using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentRider : Component, IUpdateable
{
    private float _animationTime;

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private bool _isAnimating;

    private bool _isDismounting;

    private float _outOfMountTime;

    private Vector3 _riderOffset;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private Vector3 _targetPositionOffset;

    private Quaternion _targetRotationOffset;

    public ComponentCreature ComponentCreature { get; set; } = null!;

    public ComponentMount? Mount => ComponentCreature.ComponentBody.ParentBody?.Entity.FindComponent<ComponentMount>();

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_isAnimating)
        {
            var f = 8f * dt;
            var componentBody = ComponentCreature.ComponentBody;
            componentBody.ParentBodyPositionOffset =
                Vector3.Lerp(componentBody.ParentBodyPositionOffset, _targetPositionOffset, f);
            componentBody.ParentBodyRotationOffset =
                Quaternion.Slerp(componentBody.ParentBodyRotationOffset, _targetRotationOffset, f);
            _animationTime += dt;
            if (Vector3.DistanceSquared(componentBody.ParentBodyPositionOffset, _targetPositionOffset) <
                0.0100000007f || _animationTime > 0.75f)
            {
                _isAnimating = false;
                if (_isDismounting)
                {
                    if (componentBody.ParentBody != null)
                    {
                        componentBody.Velocity = componentBody.ParentBody.Velocity;
                        componentBody.ParentBody = null;
                    }
                }
                else
                {
                    componentBody.ParentBodyPositionOffset = _targetPositionOffset;
                    componentBody.ParentBodyRotationOffset = _targetRotationOffset;
                    _outOfMountTime = 0f;
                }
            }
        }

        var mount = Mount;
        if (mount == null || _isAnimating)
        {
            return;
        }

        var componentBody2 = ComponentCreature.ComponentBody;
        var parentBody = ComponentCreature.ComponentBody.ParentBody;
        if (parentBody is null)
        {
            return;
        }

        var distance =
            Vector3.DistanceSquared(
                parentBody.Position +
                Vector3.Transform(componentBody2.ParentBodyPositionOffset, parentBody.Rotation),
                componentBody2.Position);
        if (distance > 0.160000011f)
        {
            _outOfMountTime += dt;
        }
        else
        {
            _outOfMountTime = 0f;
        }

        var componentHealth = mount.Entity.FindComponent<ComponentHealth>();
        if (_outOfMountTime > 0.1f ||
            componentHealth is { Health: <= 0f } ||
            ComponentCreature.ComponentHealth.Health <= 0f)
            //服务器和客户端都由自己控制下马
        {
            if (CommonLib.WorkType == WorkType.Local ||
                ComponentCreature.ComponentBody.Player is not { PlayerData.IsMainPlayer: false })
            {
                StartDismounting();
            }
        }

        var positionOffset = mount.MountOffset + _riderOffset;
        if (ComponentCreature.ComponentBody.IsSneaking)
        {
            positionOffset += new Vector3(0f, 0.5f, 0.1f);
        }

        ComponentCreature.ComponentBody.ParentBodyPositionOffset = positionOffset;
        ComponentCreature.ComponentBody.ParentBodyRotationOffset = Quaternion.Identity;
    }

    public ComponentMount? FindNearestMount()
    {
        var point = new Vector2(ComponentCreature.ComponentBody.Position.X, ComponentCreature.ComponentBody.Position.Z);
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(point, 2.5f, _componentBodies);
        var num = 0f;
        ComponentMount? result = null;
        foreach (var item in from b in _componentBodies
                 select b.Entity.FindComponent<ComponentMount>()
                 into m
                 where m != null && m.Entity != Entity
                 select m)
        {
            var num2 = ScoreMount(item, 2.5f);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            result = item;
        }

        return result;
    }

    public void StartNetMounting(ComponentMount componentMount)
    {
        _isAnimating = true;
        _animationTime = 0f;
        _isDismounting = false;
        ComponentCreature.ComponentBody.ParentBody = componentMount.ComponentBody;
        ComponentCreature.ComponentBody.ParentBodyPositionOffset = Vector3.Transform(
            ComponentCreature.ComponentBody.Position - componentMount.ComponentBody.Position,
            Quaternion.Conjugate(componentMount.ComponentBody.Rotation));
        ComponentCreature.ComponentBody.ParentBodyRotationOffset =
            Quaternion.Conjugate(componentMount.ComponentBody.Rotation) * ComponentCreature.ComponentBody.Rotation;
        _targetPositionOffset = componentMount.MountOffset + _riderOffset;
        _targetRotationOffset = Quaternion.Identity;
        ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled = false;
    }

    public void StartMounting(ComponentMount componentMount)
    {
        if (!_isAnimating && Mount == null)
        {
            var componentPlayer = Entity.FindComponent<ComponentPlayer>();
            if (componentPlayer != null)
            {
                var v = componentMount.ComponentBody.Position.XZ;
                if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy((int)MathUtils.Floor(v.X),
                        (int)MathUtils.Floor(v.Y), out Territoriy? territoriy))
                {
                    if (!SubsystemBedrockBlockBehavior.AllowPlayerAction(componentPlayer, territoriy!))
                    {
                        componentPlayer.ComponentGui.DisplaySmallMessage("领地内的载具不可乘骑", Color.Yellow, false, false);
                        return;
                    }
                }
            }

            if (CommonLib.WorkType != WorkType.Client)
            {
                StartNetMounting(componentMount);
                CommonLib.Net.QueuePackage(new ComponentMountPackage(this, componentMount));
            }
            else
            {
                CommonLib.Net.QueuePackage(new ComponentMountPackage(this, componentMount, true));
            }
        }
    }

    public void StartNetDismounting()
    {
        ComponentCreature.ComponentBody.Entity.FindComponent<ComponentLocomotion>();
        var x = 0f;
        if (Mount == null)
        {
            return;
        }

        if (Mount.DismountOffset.X > 0f)
        {
            var s = Mount.DismountOffset.X + 0.5f;
            var vector = 0.5f * (ComponentCreature.ComponentBody.BoundingBox.Min +
                                 ComponentCreature.ComponentBody.BoundingBox.Max);
            var terrainRaycastResult = _subsystemTerrain.Raycast(vector,
                vector - s * ComponentCreature.ComponentBody.Matrix.Right, false, true, null);
            var terrainRaycastResult2 = _subsystemTerrain.Raycast(vector,
                vector + s * ComponentCreature.ComponentBody.Matrix.Right, false, true, null);
            x = !terrainRaycastResult.HasValue
                ? -Mount.DismountOffset.X
                : !terrainRaycastResult2.HasValue
                    ? Mount.DismountOffset.X
                    : !(terrainRaycastResult.Value.Distance > terrainRaycastResult2.Value.Distance)
                        ? MathUtils.Min(terrainRaycastResult2.Value.Distance, Mount.DismountOffset.X)
                        : 0f - MathUtils.Min(terrainRaycastResult.Value.Distance, Mount.DismountOffset.X);
        }

        _isAnimating = true;
        _animationTime = 0f;
        _isDismounting = true;
        _targetPositionOffset = Mount.MountOffset + _riderOffset +
                                new Vector3(x, Mount.DismountOffset.Y, Mount.DismountOffset.Z);
        _targetRotationOffset =
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.Sign(x) * MathUtils.DegToRad(60f));
    }

    public void StartDismounting()
    {
        if (!_isAnimating && Mount != null)
        {
            if (CommonLib.WorkType != WorkType.Client)
            {
                StartNetDismounting();
                CommonLib.Net.QueuePackage(new ComponentMountPackage(this));
            }
            else
            {
                CommonLib.Net.QueuePackage(new ComponentMountPackage(this, true));
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _riderOffset = valuesDictionary.GetValue<Vector3>("RiderOffset");
    }

    public float ScoreMount(ComponentMount componentMount, float maxDistance)
    {
        if (!(componentMount.ComponentBody.Velocity.LengthSquared() < 1f))
        {
            return 0f;
        }

        var v = componentMount.ComponentBody.Position +
                Vector3.Transform(componentMount.MountOffset, componentMount.ComponentBody.Rotation) -
                ComponentCreature.ComponentCreatureModel.EyePosition;
        if (!(v.Length() < maxDistance))
        {
            return 0f;
        }

        var forward = Matrix.CreateFromQuaternion(ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
        if (Vector3.Dot(Vector3.Normalize(v), forward) > 0.33f)
        {
            return maxDistance - v.Length();
        }

        return 0f;
    }
}
