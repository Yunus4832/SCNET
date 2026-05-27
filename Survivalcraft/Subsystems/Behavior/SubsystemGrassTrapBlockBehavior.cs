namespace Game.Subsystems;

public class SubsystemGrassTrapBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private readonly List<Point3> _toRemove = [];

    private readonly Dictionary<Point3, TrapValue> _trapValues = new();

    public override int[] HandledBlocks => [87];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        foreach (var trapValue in _trapValues)
        {
            if (trapValue.Value.Damage > 1f)
            {
                for (var i = -1; i <= 1; i++)
                for (var j = -1; j <= 1; j++)
                {
                    if (MathUtils.Abs(i) + MathUtils.Abs(j) <= 1 &&
                        SubsystemTerrain.Terrain.GetCellContents(trapValue.Key.X + i, trapValue.Key.Y,
                            trapValue.Key.Z + j) == 87)
                    {
                        SubsystemTerrain.DestroyCell(0, trapValue.Key.X + i, trapValue.Key.Y, trapValue.Key.Z + j, 0,
                            false, false);
                    }
                }

                trapValue.Value.Damage = 0f;
            }
            else
            {
                trapValue.Value.Damage -= 0.5f * dt;
            }

            if (trapValue.Value.Damage <= 0f)
            {
                _toRemove.Add(trapValue.Key);
            }
        }

        foreach (var item in _toRemove)
        {
            _trapValues.Remove(item);
        }

        _toRemove.Clear();
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        if (cellFace.Face != 4 || !(componentBody.Mass > 20f))
        {
            return;
        }

        var key = new Point3(cellFace.X, cellFace.Y, cellFace.Z);
        if (!_trapValues.TryGetValue(key, out var value))
        {
            value = new TrapValue();
            _trapValues.Add(key, value);
        }

        value.Damage += 0f - velocity;
    }

    private class TrapValue
    {
        public float Damage;
    }
}
