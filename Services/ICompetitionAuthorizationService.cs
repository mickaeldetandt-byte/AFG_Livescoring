using System.Security.Claims;

namespace AFG_Livescoring.Services;

public interface ICompetitionAuthorizationService
{
    Task<bool> CanManageCompetitionAsync(
        ClaimsPrincipal principal,
        int competitionId,
        CancellationToken cancellationToken = default);
}
