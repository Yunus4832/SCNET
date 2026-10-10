using ContentServer.Domain.Announcements;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record CreateGameAnnouncementCommand(string Title, string Body) : ICommand<GameAnnouncementId>;

public sealed class CreateGameAnnouncementCommandHandler(GameAnnouncementRepository repository)
    : ICommandHandler<CreateGameAnnouncementCommand, GameAnnouncementId>
{
    public async Task<GameAnnouncementId> Handle(CreateGameAnnouncementCommand command,
        CancellationToken cancellationToken)
    {
        var announcement = GameAnnouncement.Create(command.Title, command.Body, DateTimeOffset.UtcNow);
        await repository.AddAsync(announcement, cancellationToken);
        return announcement.Id;
    }
}
