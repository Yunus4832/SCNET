using ContentServer.Domain.Announcements;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record EditGameAnnouncementCommand(GameAnnouncementId Id, string Title, string Body) : ICommand<bool>;

public sealed class EditGameAnnouncementCommandHandler(ContentServerDbContext db)
    : ICommandHandler<EditGameAnnouncementCommand, bool>
{
    public async Task<bool> Handle(EditGameAnnouncementCommand command, CancellationToken cancellationToken)
    {
        var announcement = await db.GameAnnouncements.FirstOrDefaultAsync(item => item.Id == command.Id,
            cancellationToken);
        if (announcement is null)
        {
            return false;
        }

        announcement.Edit(command.Title, command.Body, DateTimeOffset.UtcNow);
        return true;
    }
}
