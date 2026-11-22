using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentIntroPresentation : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private const string _typeName = nameof(ComponentIntro);

    private ComponentCharacter _componentCharacter = null!;

    private bool _playIntro;

    private readonly StateMachine _stateMachine = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private bool _inactive;

    private int _generation;

    private double _startTime;

    private Entity? _ship;

    private IntroCamera? _introCamera;

    private bool CanPresent => !_inactive &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation &&
                               _componentCharacter.IsLocallyControlled;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!IsAddedToProject ||
            !CanPresent ||
            !_componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.CharacterView
                .IsReadyForPlaying) { return; }

        if (_playIntro)
        {
            _playIntro = false;
            _stateMachine.TransitionTo("ShipView");
        }

        _stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        _playIntro = Entity.FindComponent<ComponentIntro>(true)!.PlayIntro;
        _stateMachine.AddState(
            "ShipView",
            ShipViewEnter,
            ShipViewUpdate,
            Actions.Empty
        );
    }

    private void ShipViewEnter()
    {
        _startTime = _subsystemTime.GameTime;
        var componentBody = _componentCharacter.Entity.FindComponent<ComponentBody>(true)!;
        var vector = ComponentIntro.FindOceanDirection(
            _subsystemTerrain.Environment,
            componentBody.Position.XZ
        );
        var searchCenter = componentBody.Position.XZ + 25f * vector;
        var isPlayerMounted = _componentCharacter.ComponentRider.Mount != null;
        var bestPosition = searchCenter;
        var bestScore = float.MinValue;
        for (var i = Terrain.ToCell(searchCenter.Y) - 15; i < Terrain.ToCell(searchCenter.Y) + 15; i++)
        {
            for (var j = Terrain.ToCell(searchCenter.X) - 15; j < Terrain.ToCell(searchCenter.X) + 15; j++)
            {
                var candidateScore = ScoreShipPosition(componentBody.Position.XZ, j, i);
                if (!(candidateScore > bestScore))
                {
                    continue;
                }

                bestScore = candidateScore;
                bestPosition = new Vector2(j, i);
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
        var shipPosition = new Vector3(bestPosition.X, _subsystemTerrain.Environment.OceanLevel + 0.5f, bestPosition.Y);
        entity.FindComponent<ComponentFrame>(true)!.Position = shipPosition;
        entity.FindComponent<ComponentIntroShip>(true)!.Heading = Vector2.Angle(vector, -Vector2.UnitY);
        Project.AddEntity(entity);
        _ship = entity;
        QueueMessage(
            2.0,
            delegate
            {
                _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.DisplayLargeMessage(
                    string.Empty,
                    Localization.ClientLanguage.Catalog.Get(_typeName, 1),
                    5f,
                    0f
                );
            }
        );
        QueueMessage(
            7.0,
            delegate
            {
                _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.DisplayLargeMessage(
                    string.Empty,
                    isPlayerMounted
                        ? Localization.ClientLanguage.Catalog.Get(_typeName, 2)
                        : Localization.ClientLanguage.Catalog.Get(_typeName, 3),
                    5f,
                    0f
                );
            }
        );
        QueueMessage(
            12.0,
            delegate
            {
                _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.DisplayLargeMessage(
                    string.Empty,
                    Localization.ClientLanguage.Catalog.Get(_typeName, 4),
                    5f,
                    0f
                );
            }
        );
        var introCamera = _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
            .FindCamera<IntroCamera>()!;
        _introCamera = introCamera;
        _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.ActiveCamera =
            introCamera;
        introCamera.CameraPosition = shipPosition +
                                     new Vector3(12f * vector.X, 8f, 12f * vector.Y) +
                                     new Vector3(-5f * vector.Y, 0f, 5f * vector.X);
        introCamera.TargetPosition = _componentCharacter.ComponentCreaturePose.EyePosition +
                                     2.5f * new Vector3(vector.X, 0f, vector.Y);
        introCamera.Speed = 0f;
        introCamera.TargetCameraPosition = _componentCharacter.ComponentCreaturePose.EyePosition;
    }

    private void ShipViewUpdate()
    {
        var introCamera = _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
            .FindCamera<IntroCamera>()!;
        introCamera.Speed = MathUtils.Lerp(
            0f,
            8f,
            MathUtils.Saturate(((float)(_subsystemTime.GameTime - _startTime) - 6f) / 3f)
        );
        if (!(Vector3.Distance(introCamera.TargetCameraPosition, introCamera.CameraPosition) < 0.3f))
        {
            return;
        }

        _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.ActiveCamera =
            _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                .FindCamera<FppCamera>()!;
        _stateMachine.TransitionTo(string.Empty);
    }

    private float ScoreShipPosition(Vector2 playerPosition, int x, int z)
    {
        var score = 0f;
        var shoreDistance = _subsystemTerrain.Environment.CalculateOceanShoreDistance(x, z);
        if (shoreDistance > -8f)
        {
            score -= 100f;
        }

        score -= 0.25f * shoreDistance;
        var playerDistance = Vector2.Distance(playerPosition, new Vector2(x, z));
        score -= MathUtils.Abs(playerDistance - 20f);
        var waterDepth = 0;
        var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell is { HasCurrentHeightData: true })
        {
            var oceanLevel = _subsystemTerrain.Environment.OceanLevel;
            var sampleY = oceanLevel;
            while (sampleY >= oceanLevel - 5 && sampleY >= 0)
            {
                var cellContentsFast = chunkAtCell.GetCellContentsFast(x & 0xF, sampleY, z & 0xF);
                if (cellContentsFast != 18 && cellContentsFast != 92)
                {
                    break;
                }

                sampleY--;
                waterDepth++;
            }
        }
        else
        {
            waterDepth = 2;
        }

        if (waterDepth < 2)
        {
            score -= 100f;
        }

        return score + 2f * waterDepth;
    }

    private void QueueMessage(double delay, Action action)
    {
        var generation = _generation;
        _subsystemTime.QueueGameTimeDelayedExecution(
            _startTime + delay,
            () =>
            {
                if (generation == _generation && CanPresent) { action(); }
            }
        );
    }

    public override void OnEntityAdded() => _inactive = false;

    private void StopPresentation()
    {
        _inactive = true;
        _generation++;
        _playIntro = false;
        _stateMachine.TransitionTo(string.Empty);
        if (WorldRuntimeContext.IsProjectActive(Project))
        {
            if (_introCamera is not null &&
                ReferenceEquals(
                    _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                        .ActiveCamera,
                    _introCamera
                ))
            {
                _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                        .ActiveCamera =
                    _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                        .FindCamera<FppCamera>()!;
            }

            if (_ship is { IsAddedToProject: true }) { Project.RemoveEntity(_ship, true); }
        }

        _ship = null;
        _introCamera = null;
    }

    public override void OnEntityRemoved() => StopPresentation();
    public override void Dispose() => StopPresentation();
}
