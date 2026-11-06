namespace Game.World;

public interface IUpdateable
{
    UpdateExecutionSide UpdateSide { get; }

    UpdateOrder UpdateOrder { get; }

    void Update(float dt);
}
