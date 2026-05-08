namespace Game.Cameras;

public class RandomJumpCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    public const float FrequencyFactor = 0.5f;

    private float _frequency = 0.5f;

    private readonly Random _innerRandom = new();

    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => false;

    public override void Activate(Camera previousCamera)
    {
        SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        if (_innerRandom.Float(0f, 1f) < 0.1f * dt)
        {
            _frequency = _innerRandom.Float(0.33f, 5f) * 0.5f;
        }

        if (_innerRandom.Float(0f, 1f) < _frequency * dt)
        {
            var subsystemPlayers = GameWidget.SubsystemGameWidgets.Project.FindSubsystem<SubsystemPlayers>(true)!;
            if (subsystemPlayers.PlayersData.Count > 0)
            {
                var spawnPosition = subsystemPlayers.PlayersData[0].SpawnPosition;
                spawnPosition.X += _innerRandom.Float(-150f, 150f);
                spawnPosition.Y = _innerRandom.Float(70f, 120f);
                spawnPosition.Z += _innerRandom.Float(-150f, 150f);
                var direction = _innerRandom.Vector3(1f);
                SetupPerspectiveCamera(spawnPosition, direction, Vector3.UnitY);
            }
        }

        if (_innerRandom.Float(0f, 1f) < 0.5f * _frequency * dt)
        {
            GameWidget.SubsystemGameWidgets.Project.FindSubsystem<SubsystemTimeOfDay>(true)!.TimeOfDayOffset =
                _innerRandom.Float(0f, 1f);
        }

        if (_innerRandom.Float(0f, 1f) < 1f * dt * 0.5f)
        {
            GameManager.SaveProject(false, false);
        }
    }
}
