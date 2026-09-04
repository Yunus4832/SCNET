using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.TerrainSerializers;

namespace Game.Components;

public class ComponentIntro : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentIntro);

    private ComponentPlayer _componentPlayer = null!;

    private bool _playIntro;

    private readonly StateMachine _stateMachine = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_playIntro)
        {
            _playIntro = false;
            _stateMachine.TransitionTo("ShipView");
        }

        _stateMachine.Update();
    }

    public static Vector2 FindOceanDirection(ITerrainContentsGenerator generator, Vector2 position)
    {
        var num = float.MaxValue;
        var result = Vector2.Zero;
        for (var i = 0; i < 36; i++)
        {
            var vector = Vector2.CreateFromAngle(i / 36f * 2f * (float)Math.PI);
            var vector2 = position + 50f * vector;
            var num2 = generator.CalculateOceanShoreDistance(vector2.X, vector2.Y);
            if (!(num2 < num))
            {
                continue;
            }

            result = vector;
            num = num2;
        }

        return result;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        _playIntro = valuesDictionary.GetValue<bool>("PlayIntro");
        _stateMachine.AddState(
            "ShipView",
            ShipViewEnter,
            ShipViewUpdate,
            Actions.Empty
        );
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("PlayIntro", _playIntro);
    }

    public void ShipViewEnter()
    {
        var componentBody = _componentPlayer.Entity.FindComponent<ComponentBody>(true)!;
        var vector = FindOceanDirection(_subsystemTerrain.TerrainContentsGenerator, componentBody.Position.XZ);
        var vector2 = componentBody.Position.XZ + 25f * vector;
        var isPlayerMounted = _componentPlayer.ComponentRider.Mount != null;
        var vector3 = vector2;
        var num = float.MinValue;
        for (var i = Terrain.ToCell(vector2.Y) - 15; i < Terrain.ToCell(vector2.Y) + 15; i++)
        {
            for (var j = Terrain.ToCell(vector2.X) - 15; j < Terrain.ToCell(vector2.X) + 15; j++)
            {
                var num2 = ScoreShipPosition(componentBody.Position.XZ, j, i);
                if (!(num2 > num))
                {
                    continue;
                }

                num = num2;
                vector3 = new Vector2(j, i);
            }
        }

        var databaseObject = Project.GameDatabase.Database.FindDatabaseObject(
            "IntroShip",
            Project.GameDatabase.EntityTemplateType,
            true
        )!;
        var valuesDictionary = new ValuesDictionary();
        valuesDictionary.PopulateFromDatabaseObject(databaseObject);
        var entity = Project.CreateEntity(valuesDictionary);
        var vector4 = new Vector3(vector3.X, _subsystemTerrain.TerrainContentsGenerator.OceanLevel + 0.5f, vector3.Y);
        entity.FindComponent<ComponentFrame>(true)!.Position = vector4;
        entity.FindComponent<ComponentIntroShip>(true)!.Heading = Vector2.Angle(vector, -Vector2.UnitY);
        Project.AddEntity(entity);
        _subsystemTime.QueueGameTimeDelayedExecution(
            2.0,
            delegate
            {
                _componentPlayer.ComponentGui.DisplayLargeMessage(
                    string.Empty,
                    LanguageManager.Get(_typeName, 1),
                    5f,
                    0f
                );
            }
        );
        _subsystemTime.QueueGameTimeDelayedExecution(
            7.0,
            delegate
            {
                _componentPlayer.ComponentGui.DisplayLargeMessage(
                    string.Empty,
                    isPlayerMounted
                        ? LanguageManager.Get(_typeName, 2)
                        : LanguageManager.Get(_typeName, 3),
                    5f,
                    0f
                );
            }
        );
        _subsystemTime.QueueGameTimeDelayedExecution(
            12.0,
            delegate
            {
                _componentPlayer.ComponentGui.DisplayLargeMessage(
                    string.Empty,
                    LanguageManager.Get(_typeName, 4),
                    5f,
                    0f
                );
            }
        );
        var introCamera = _componentPlayer.GameWidget.FindCamera<IntroCamera>()!;
        _componentPlayer.GameWidget.ActiveCamera = introCamera;
        introCamera.CameraPosition = vector4 + new Vector3(12f * vector.X, 8f, 12f * vector.Y) +
                                     new Vector3(-5f * vector.Y, 0f, 5f * vector.X);
        introCamera.TargetPosition = _componentPlayer.ComponentCreatureModel.EyePosition +
                                     2.5f * new Vector3(vector.X, 0f, vector.Y);
        introCamera.Speed = 0f;
        introCamera.TargetCameraPosition = _componentPlayer.ComponentCreatureModel.EyePosition;
    }

    public void ShipViewUpdate()
    {
        var introCamera = _componentPlayer.GameWidget.FindCamera<IntroCamera>()!;
        introCamera.Speed = MathUtils.Lerp(0f, 8f,
            MathUtils.Saturate(((float)_subsystemGameInfo.TotalElapsedGameTime - 6f) / 3f));
        if (!(Vector3.Distance(introCamera.TargetCameraPosition, introCamera.CameraPosition) < 0.3f))
        {
            return;
        }

        _componentPlayer.GameWidget.ActiveCamera = _componentPlayer.GameWidget.FindCamera<FppCamera>()!;
        _stateMachine.TransitionTo(string.Empty);
    }

    public float ScoreShipPosition(Vector2 playerPosition, int x, int z)
    {
        var num = 0f;
        var num2 = _subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(x, z);
        if (num2 > -8f)
        {
            num -= 100f;
        }

        num -= 0.25f * num2;
        var num3 = Vector2.Distance(playerPosition, new Vector2(x, z));
        num -= MathUtils.Abs(num3 - 20f);
        var num4 = 0;
        var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell is { MainThreadState: >= TerrainChunkState.InvalidLight })
        {
            var oceanLevel = _subsystemTerrain.TerrainContentsGenerator.OceanLevel;
            var num5 = oceanLevel;
            while (num5 >= oceanLevel - 5 && num5 >= 0)
            {
                var cellContentsFast = chunkAtCell.GetCellContentsFast(x & 0xF, num5, z & 0xF);
                if (cellContentsFast != 18 && cellContentsFast != 92)
                {
                    break;
                }

                num5--;
                num4++;
            }
        }
        else
        {
            num4 = 2;
        }

        if (num4 < 2)
        {
            num -= 100f;
        }

        return num + 2f * num4;
    }
}
