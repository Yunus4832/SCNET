using ContentServer.Domain.Announcements;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SetGameAnnouncementStatusCommand(GameAnnouncementId Id, bool Publish) : ICommand<bool>;

public sealed class SetGameAnnouncementStatusCommandHandler(ContentServerDbContext db)
    : ICommandHandler<SetGameAnnouncementStatusCommand, bool>
{
    public async Task<bool> Handle(SetGameAnnouncementStatusCommand command, CancellationToken cancellationToken)
    {
        var announcement = await db.GameAnnouncements.FirstOrDefaultAsync(item => item.Id == command.Id,
            cancellationToken);
        if (announcement is null)
        {
            return false;
        }

        if (command.Publish)
        {
            announcement.Publish(DateTimeOffset.UtcNow);
        }
        else
        {
            announcement.Withdraw(DateTimeOffset.UtcNow);
        }

        return true;
    }
}
