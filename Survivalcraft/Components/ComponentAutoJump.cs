using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentAutoJump : Component, IUpdateable
{
    private bool _alwaysEnabled;

    private bool _collidedWithBody;

    private float _jumpStrength;

    private double _lastAutoJumpTime;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentCreature _componentCreature = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if ((SettingsManager.Current.AutoJump || _alwaysEnabled) && _subsystemTime.GameTime - _lastAutoJumpTime > 0.25)
        {
            var lastWalkOrder = _componentCreature.ComponentLocomotion.LastWalkOrder;
            if (lastWalkOrder.HasValue)
            {
                var vector = new Vector2(_componentCreature.ComponentBody.CollisionVelocityChange.X,
                    _componentCreature.ComponentBody.CollisionVelocityChange.Z);
                if (vector != Vector2.Zero && !_collidedWithBody)
                {
                    var v = Vector2.Normalize(vector);
                    var vector2 = _componentCreature.ComponentBody.Matrix.Right * lastWalkOrder.Value.X +
                                  _componentCreature.ComponentBody.Matrix.Forward * lastWalkOrder.Value.Y;
                    var v2 = Vector2.Normalize(new Vector2(vector2.X, vector2.Z));
                    var flag = false;
                    var v3 = Vector3.Zero;
                    var vector3 = Vector3.Zero;
                    var vector4 = Vector3.Zero;
                    if (Vector2.Dot(v2, -v) > 0.6f)
                    {
                        if (Vector2.Dot(v2, Vector2.UnitX) > 0.6f)
                        {
                            v3 = _componentCreature.ComponentBody.Position + Vector3.UnitX;
                            vector3 = v3 - Vector3.UnitZ;
                            vector4 = v3 + Vector3.UnitZ;
                            flag = true;
                        }
                        else if (Vector2.Dot(v2, -Vector2.UnitX) > 0.6f)
                        {
                            v3 = _componentCreature.ComponentBody.Position - Vector3.UnitX;
                            vector3 = v3 - Vector3.UnitZ;
                            vector4 = v3 + Vector3.UnitZ;
                            flag = true;
                        }
                        else if (Vector2.Dot(v2, Vector2.UnitY) > 0.6f)
                        {
                            v3 = _componentCreature.ComponentBody.Position + Vector3.UnitZ;
                            vector3 = v3 - Vector3.UnitX;
                            vector4 = v3 + Vector3.UnitX;
                            flag = true;
                        }
                        else if (Vector2.Dot(v2, -Vector2.UnitY) > 0.6f)
                        {
                            v3 = _componentCreature.ComponentBody.Position - Vector3.UnitZ;
                            vector3 = v3 - Vector3.UnitX;
                            vector4 = v3 + Vector3.UnitX;
                            flag = true;
                        }
                    }

                    if (flag)
                    {
                        var cellContents = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(v3.X),
                            Terrain.ToCell(v3.Y), Terrain.ToCell(v3.Z));
                        var cellContents2 = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(vector3.X),
                            Terrain.ToCell(vector3.Y), Terrain.ToCell(vector3.Z));
                        var cellContents3 = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(vector4.X),
                            Terrain.ToCell(vector4.Y), Terrain.ToCell(vector4.Z));
                        var cellContents4 = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(v3.X),
                            Terrain.ToCell(v3.Y) + 1, Terrain.ToCell(v3.Z));
                        var cellContents5 = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(vector3.X),
                            Terrain.ToCell(vector3.Y) + 1, Terrain.ToCell(vector3.Z));
                        var cellContents6 = _subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(vector4.X),
                            Terrain.ToCell(vector4.Y) + 1, Terrain.ToCell(vector4.Z));
                        var block = BlocksManager.Blocks[cellContents];
                        var block2 = BlocksManager.Blocks[cellContents2];
                        var block3 = BlocksManager.Blocks[cellContents3];
                        var block4 = BlocksManager.Blocks[cellContents4];
                        var block5 = BlocksManager.Blocks[cellContents5];
                        var block6 = BlocksManager.Blocks[cellContents6];
                        if (!block.NoAutoJump && ((block.Collidable && !block4.Collidable) ||
                                                  (block2.Collidable && !block5.Collidable) ||
                                                  (block3.Collidable && !block6.Collidable)))
                        {
                            _componentCreature.ComponentLocomotion.JumpOrder = MathUtils.Max(_jumpStrength,
                                _componentCreature.ComponentLocomotion.JumpOrder);
                            _lastAutoJumpTime = _subsystemTime.GameTime;
                        }
                    }
                }
            }
        }

        _collidedWithBody = false;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _alwaysEnabled = valuesDictionary.GetValue<bool>("AlwaysEnabled");
        _jumpStrength = valuesDictionary.GetValue<float>("JumpStrength");
        _componentCreature.ComponentBody.CollidedWithBody += delegate { _collidedWithBody = true; };
    }
}
