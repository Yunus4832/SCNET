namespace Game.ElectricElements;

public class MotionDetectorElectricElement : MountedElectricElement
{
    private const float _range = 8f;

    private const float _speedThreshold = 0.25f;

    private const float _pollingPeriod = 0.25f;

    private readonly DynamicArray<ComponentBody> _bodies = [];

    private readonly Vector3 _center;

    private readonly Vector2 _corner1;

    private readonly Vector2 _corner2;

    private readonly Vector3 _direction;

    private readonly SubsystemBodies _subsystemBodies;

    private readonly SubsystemMovingBlocks _subsystemMovingBlocks;

    private readonly SubsystemPickables _subsystemPickables;

    private readonly SubsystemProjectiles _subsystemProjectiles;

    private float _voltage;

    public MotionDetectorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemBodies = subsystemElectricity.Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemMovingBlocks = subsystemElectricity.Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemProjectiles = subsystemElectricity.Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemPickables = subsystemElectricity.Project.FindSubsystem<SubsystemPickables>(true)!;
        _center = new Vector3(cellFace.X, cellFace.Y, cellFace.Z) + new Vector3(0.5f) - 0.25f * _direction;
        _direction = CellFace.FaceToVector3(cellFace.Face);
        var vector = Vector3.One - new Vector3(MathUtils.Abs(_direction.X), MathUtils.Abs(_direction.Y),
            MathUtils.Abs(_direction.Z));
        var vector2 = _center - 8f * vector;
        var vector3 = _center + 8f * (vector + _direction);
        _corner1 = new Vector2(vector2.X, vector2.Z);
        _corner2 = new Vector2(vector3.X, vector3.Z);
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        _voltage = CalculateMotionVoltage();
        if (_voltage > 0f && voltage == 0f)
        {
            SubsystemElectricity.SubsystemAudio.PlaySound("Audio/MotionDetectorClick", 1f, 0f, _center, 1f, true);
        }

        var num = 0.25f * (0.9f + 0.000200000009f * (GetHashCode() % 1000));
        SubsystemElectricity.QueueElectricElementForSimulation(this,
            SubsystemElectricity.CircuitStep + MathUtils.Max((int)(num / 0.01f), 1));
        return _voltage.UncloseTo(voltage);
    }

    public float CalculateMotionVoltage()
    {
        var num = 0f;
        _bodies.Clear();
        _subsystemBodies.FindBodiesInArea(_corner1, _corner2, _bodies);
        for (var i = 0; i < _bodies.Count; i++)
        {
            var componentBody = _bodies.Array[i];
            if (!(componentBody.Velocity.LengthSquared() < 0.0625f))
            {
                num = MathUtils.Max(num,
                    TestPoint(componentBody.Position + new Vector3(0f, 0.5f * componentBody.BoxSize.Y, 0f)));
            }
        }

        foreach (var movingBlockSet in _subsystemMovingBlocks.ReadonlyMovingBlockSets)
        {
            if (movingBlockSet.CurrentVelocity.LengthSquared() < 0.0625f ||
                BoundingBox.Distance(movingBlockSet.BoundingBox(false), _center) > 8f)
            {
                continue;
            }

            foreach (var block in movingBlockSet.Blocks)
            {
                num = MathUtils.Max(num,
                    TestPoint(movingBlockSet.Position + new Vector3(block.Offset) + new Vector3(0.5f)));
            }
        }

        foreach (var projectile in _subsystemProjectiles.Projectiles)
        {
            if (!(projectile.Velocity.LengthSquared() < 0.0625f))
            {
                num = MathUtils.Max(num, TestPoint(projectile.Position));
            }
        }

        foreach (var pickable in _subsystemPickables.Pickables)
        {
            if (!(pickable.Velocity.LengthSquared() < 0.0625f))
            {
                num = MathUtils.Max(num, TestPoint(pickable.Position));
            }
        }

        return !(num > 0f) ? 0f : MathUtils.Lerp(0.51f, 1f, MathUtils.Saturate(num * 1.1f));
    }

    public float TestPoint(Vector3 p)
    {
        var num = Vector3.DistanceSquared(p, _center);
        if (num < 64f && Vector3.Dot(Vector3.Normalize(p - (_center - 0.75f * _direction)), _direction) > 0.5f &&
            !SubsystemElectricity.SubsystemTerrain.Raycast(_center, p, false, true, delegate (int value, float _)
            {
                var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
                return block.Collidable && block.BlockIndex != 15 && block.BlockIndex != 60 &&
                       block.BlockIndex != 44 && block.BlockIndex != 18;
            }).HasValue)
        {
            return MathUtils.Saturate(1f - MathUtils.Sqrt(num) / 8f);
        }

        return 0f;
    }
}
