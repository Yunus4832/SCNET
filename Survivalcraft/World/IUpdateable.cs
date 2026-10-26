namespace Game.World;

public interface IUpdateable
{
    UpdateOrder UpdateOrder { get; }

    void Update(float dt);
}
