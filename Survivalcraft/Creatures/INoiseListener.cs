namespace Game.Creatures;

public interface INoiseListener
{
    void HearNoise(ComponentBody? sourceBody, Vector3 sourcePosition, float loudness);
}
