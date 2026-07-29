using System.Security.Claims;
using AFG_Livescoring.Models;
using Microsoft.EntityFrameworkCore;

namespace AFG_Livescoring.Services;

public sealed class CompetitionAuthorizationService : ICompetitionAuthorizationService
{
    private readonly AppDbContext _db;

    public CompetitionAuthorizationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanManageCompetitionAsync(
        ClaimsPrincipal principal,
        int competitionId,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        var currentUser = await _db.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Role,
                user.ClubId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (currentUser == null)
        {
            return false;
        }

        var competition = await _db.Competitions
            .AsNoTracking()
            .Where(competition => competition.Id == competitionId)
            .Select(competition => new
            {
                competition.ClubId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (competition == null)
        {
            return false;
        }

        if (string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(currentUser.Role, "Club", StringComparison.OrdinalIgnoreCase)
               && currentUser.ClubId.HasValue
               && competition.ClubId.HasValue
               && currentUser.ClubId.Value == competition.ClubId.Value;
    }
}
