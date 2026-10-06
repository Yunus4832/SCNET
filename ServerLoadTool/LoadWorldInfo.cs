using EntitySystem.TemplatesDatabase;

using Game;

namespace ServerLoadTool;

public sealed record LoadWorldInfo(string Name, int Seed, string Mode, ushort MaximumPlayers)
{
    public static LoadWorldInfo Read(byte[] projectData)
    {
        var project = new ValuesDictionary();
        project.ApplyOverridesUseMessagePack(projectData);
        var info = project.GetValue<ValuesDictionary>("Subsystems").GetValue<ValuesDictionary>("GameInfo");
        var mode = info.GetValue<GameMode>("GameMode");
        if (mode != GameMode.Creative)
        {
            throw new InvalidOperationException("Load clients require a Creative test world; other game modes are not supported.");
        }

        return new LoadWorldInfo(info.GetValue<string>("WorldName"), info.GetValue<int>("WorldSeed"), mode.ToString(),
            info.GetValue<ushort>("MaxOnlinePlayerCount"));
    }
}
