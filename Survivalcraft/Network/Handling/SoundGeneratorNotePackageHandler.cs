using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class SoundGeneratorNotePackageHandler : PackageHandlerBase<SoundGeneratorNotePackage>
{
    public override void Handle(SoundGeneratorNotePackage package, PackageReceiveContext context)
    {
        if (context.IsServer ||
            context.Project is not { } project ||
            !context.Runtime.HasPresentation ||
            !WorldRuntimeContext.IsProjectActive(project) ||
            !package.Note.IsValid)
        {
            return;
        }

        project.FindSubsystem<SubsystemSoundGeneratorPresentation>(true)!.PlayNote(package.Note);
    }
}
