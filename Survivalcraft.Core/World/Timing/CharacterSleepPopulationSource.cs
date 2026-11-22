namespace Game.World.Timing;

/// <summary>游戏时间回调执行后读取当前实际可玩的角色数量。</summary>
public sealed class CharacterSleepPopulationSource(SubsystemCharacters characters)
{
    public CharacterSleepPopulation ReadPopulation()
    {
        var sleeping = 0;
        var dead = 0;
        var population = characters.ComponentCharacters;
        foreach (var character in population)
        {
            if (character.ComponentHealth.Health == 0f) { dead++; }
            else if (character.ComponentSleep.SleepFactor.CloseTo(1f)) { sleeping++; }
        }

        return new CharacterSleepPopulation(population.Count, sleeping, dead);
    }
}
