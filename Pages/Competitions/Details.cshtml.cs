using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ICompetitionAuthorizationService _authorizationService;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(
            AppDbContext db,
            ICompetitionAuthorizationService authorizationService,
            ILogger<DetailsModel>? logger = null)
        {
            _db = db;
            _authorizationService = authorizationService;
            _logger = logger ?? NullLogger<DetailsModel>.Instance;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Competition? Competition { get; set; }
        public Course? Course => Competition?.Course;

        public int PlayerCount { get; set; }
        public int SquadCount { get; set; }
        public bool HasStarted { get; set; }
        public int CompletedRounds { get; set; }
        public int TotalRounds { get; set; }
        public bool IsSportingStructureComplete { get; set; }

        public bool IsTraining =>
            string.Equals(Competition?.Mode, "Training", StringComparison.OrdinalIgnoreCase);

        public bool IsCompetition => !IsTraining;

        public bool CanManage { get; set; }
        public bool CanStart { get; set; }
        public bool CanFinish { get; set; }

        public bool ShowLeaderboardButton => Competition != null && Competition.Status != CompetitionStatus.Draft;
        public bool ShowResultsButton => Competition?.Status == CompetitionStatus.Finished;
        public bool ShowSquadsButton => CanManage;
        public bool ShowParticipantsButton => CanManage;
        public bool ShowLiveButton => Competition?.Status == CompetitionStatus.InProgress;

        public async Task<IActionResult> OnGetAsync()
        {
            var authorizationFailure = await GetAuthorizationFailureAsync(
                Id,
                "ViewDetails");
            if (authorizationFailure != null)
                return authorizationFailure;

            if (!await LoadPageDataAsync())
                return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostStartAsync(int id)
        {
            var authorizationFailure = await GetAuthorizationFailureAsync(
                id,
                "Start");
            if (authorizationFailure != null)
                return authorizationFailure;

            var competition = await _db.Competitions
                .SingleOrDefaultAsync(
                    item => item.Id == id,
                    HttpContext.RequestAborted);

            if (competition == null)
                return NotFound();

            if (competition.Status != CompetitionStatus.Draft)
            {
                LogStatusTransitionRefused(
                    "Start",
                    competition,
                    CompetitionStatus.InProgress,
                    "InvalidCurrentStatus");
                TempData["Error"] =
                    "Seule une compétition en brouillon peut être démarrée.";
                return RedirectToPage(new { id });
            }

            if (!await HasMinimumStartStructureAsync(competition))
            {
                LogStatusTransitionRefused(
                    "Start",
                    competition,
                    CompetitionStatus.InProgress,
                    "IncompleteStructure");
                TempData["Error"] =
                    "Impossible de démarrer : la structure et les participants de la compétition sont incomplets.";
                return RedirectToPage(new { id });
            }

            var previousStatus = competition.Status;
            competition.Status = CompetitionStatus.InProgress;
            try
            {
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Competition operation {Operation} failed for CompetitionId {CompetitionId} by UserId {UserId}, Role {Role}, ClubId {ClubId}, OldStatus {OldStatus}, NewStatus {NewStatus}",
                    "Start",
                    competition.Id,
                    GetCurrentUserIdentifier(),
                    GetCurrentRole(),
                    competition.ClubId,
                    previousStatus,
                    CompetitionStatus.InProgress);
                throw;
            }

            LogStatusTransitionSucceeded(
                "Start",
                competition,
                previousStatus,
                CompetitionStatus.InProgress);
            TempData["SuccessMessage"] = "Compétition démarrée.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostFinishAsync(int id)
        {
            var authorizationFailure = await GetAuthorizationFailureAsync(
                id,
                "Finish");
            if (authorizationFailure != null)
                return authorizationFailure;

            var competition = await _db.Competitions
                .SingleOrDefaultAsync(
                    item => item.Id == id,
                    HttpContext.RequestAborted);

            if (competition == null)
                return NotFound();

            if (competition.Status != CompetitionStatus.InProgress)
            {
                LogStatusTransitionRefused(
                    "Finish",
                    competition,
                    CompetitionStatus.Finished,
                    "InvalidCurrentStatus");
                TempData["Error"] =
                    "Seule une compétition en cours peut être terminée.";
                return RedirectToPage(new { id });
            }

            var previousStatus = competition.Status;
            competition.Status = CompetitionStatus.Finished;
            try
            {
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Competition operation {Operation} failed for CompetitionId {CompetitionId} by UserId {UserId}, Role {Role}, ClubId {ClubId}, OldStatus {OldStatus}, NewStatus {NewStatus}",
                    "Finish",
                    competition.Id,
                    GetCurrentUserIdentifier(),
                    GetCurrentRole(),
                    competition.ClubId,
                    previousStatus,
                    CompetitionStatus.Finished);
                throw;
            }

            LogStatusTransitionSucceeded(
                "Finish",
                competition,
                previousStatus,
                CompetitionStatus.Finished);
            TempData["SuccessMessage"] = "Compétition terminée avec succès.";
            return RedirectToPage(new { id });
        }

        private async Task<IActionResult?> GetAuthorizationFailureAsync(
            int competitionId,
            string operation)
        {
            var canManageCompetition = await _authorizationService
                .CanManageCompetitionAsync(
                    User,
                    competitionId,
                    HttpContext.RequestAborted);

            if (canManageCompetition)
                return null;

            var competitionScope = await _db.Competitions
                .AsNoTracking()
                .Where(competition => competition.Id == competitionId)
                .Select(competition => new { competition.ClubId })
                .SingleOrDefaultAsync(
                    HttpContext.RequestAborted);

            if (competitionScope != null)
            {
                _logger.LogWarning(
                    "Competition operation {Operation} refused for CompetitionId {CompetitionId} by UserId {UserId}, Role {Role}, ClubId {ClubId}, Reason {Reason}",
                    operation,
                    competitionId,
                    GetCurrentUserIdentifier(),
                    GetCurrentRole(),
                    competitionScope.ClubId,
                    "CrossClubAccess");
                return Forbid();
            }

            return NotFound();
        }

        private void LogStatusTransitionSucceeded(
            string operation,
            Competition competition,
            CompetitionStatus oldStatus,
            CompetitionStatus newStatus)
        {
            _logger.LogInformation(
                "Competition operation {Operation} succeeded for CompetitionId {CompetitionId} by UserId {UserId}, Role {Role}, ClubId {ClubId}, OldStatus {OldStatus}, NewStatus {NewStatus}",
                operation,
                competition.Id,
                GetCurrentUserIdentifier(),
                GetCurrentRole(),
                competition.ClubId,
                oldStatus,
                newStatus);
        }

        private void LogStatusTransitionRefused(
            string operation,
            Competition competition,
            CompetitionStatus requestedStatus,
            string reason)
        {
            _logger.LogWarning(
                "Competition operation {Operation} refused for CompetitionId {CompetitionId} by UserId {UserId}, Role {Role}, ClubId {ClubId}, OldStatus {OldStatus}, NewStatus {NewStatus}, Reason {Reason}",
                operation,
                competition.Id,
                GetCurrentUserIdentifier(),
                GetCurrentRole(),
                competition.ClubId,
                competition.Status,
                requestedStatus,
                reason);
        }

        private string GetCurrentUserIdentifier() =>
            User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.Identity?.Name
            ?? "anonymous";

        private string GetCurrentRole() =>
            User.FindFirstValue(System.Security.Claims.ClaimTypes.Role)
            ?? "unknown";

        private async Task<bool> LoadPageDataAsync()
        {
            Competition = await _db.Competitions
                .Include(c => c.Course)
                .Include(c => c.Club)
                .Include(c => c.CreatedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Id == Id,
                    HttpContext.RequestAborted);

            if (Competition == null)
                return false;

            var metricsByCompetition =
                await CompetitionMetricsCalculator.CalculateAsync(
                    _db,
                    new[] { Competition },
                    HttpContext.RequestAborted);
            var metrics = metricsByCompetition[Competition.Id];

            SquadCount = await _db.Squads
                .AsNoTracking()
                .CountAsync(
                    squad => squad.CompetitionId == Id,
                    HttpContext.RequestAborted);
            PlayerCount = metrics.ParticipantsCount;
            HasStarted = metrics.HasStarted;
            CompletedRounds = metrics.CompletedRounds;
            TotalRounds = metrics.TotalRounds;
            IsSportingStructureComplete = TotalRounds > 0
                                          && CompletedRounds == TotalRounds;

            CanManage = true;
            CanStart = Competition.Status == CompetitionStatus.Draft;
            CanFinish = Competition.Status == CompetitionStatus.InProgress;

            if (Competition.Status == CompetitionStatus.Draft && HasStarted)
            {
                CanStart = false;
                CanFinish = false;
            }

            return true;
        }

        private async Task<bool> HasMinimumStartStructureAsync(Competition competition)
        {
            var rounds = await _db.Rounds
                .AsNoTracking()
                .Where(round => round.CompetitionId == competition.Id
                                && round.SquadId.HasValue
                                && round.Player != null)
                .Select(round => new
                {
                    round.PlayerId,
                    SquadId = round.SquadId!.Value
                })
                .ToListAsync(HttpContext.RequestAborted);

            if (rounds.Count == 0)
                return false;

            var isDoubles = competition.CompetitionType is
                CompetitionType.DoublesScramble
                or CompetitionType.DoublesFourball
                or CompetitionType.DoublesFoursome
                or CompetitionType.MatchPlayFourball
                or CompetitionType.MatchPlayFoursome
                or CompetitionType.MatchPlayScramble;

            if (!isDoubles)
            {
                var minimumPlayers = competition.CompetitionType
                    == CompetitionType.MatchPlayIndividual
                    ? 2
                    : 1;

                return rounds
                    .Select(round => round.PlayerId)
                    .Distinct()
                    .Count() >= minimumPlayers;
            }

            var validTeams = await _db.Teams
                .AsNoTracking()
                .Where(team => team.CompetitionId == competition.Id
                               && team.SquadId.HasValue
                               && team.IsActive)
                .Select(team => new
                {
                    team.Id,
                    team.SquadId,
                    PlayerIds = team.TeamPlayers
                        .Select(teamPlayer => teamPlayer.PlayerId)
                        .ToList(),
                    HasTeamRound = team.TeamRounds.Any(
                        teamRound => teamRound.CompetitionId == competition.Id
                                     && teamRound.SquadId == team.SquadId)
                })
                .ToListAsync(HttpContext.RequestAborted);

            return validTeams
                .Where(team =>
                    team.HasTeamRound
                    && team.PlayerIds.Count == 2
                    && team.PlayerIds.Distinct().Count() == 2
                    && team.PlayerIds.All(playerId =>
                        rounds.Any(round =>
                            round.PlayerId == playerId
                            && round.SquadId == team.SquadId)))
                .GroupBy(team => team.SquadId)
                .Any(group => group.Count() >= 2);
        }

        public string FormatStatus(CompetitionStatus status)
        {
            return status switch
            {
                CompetitionStatus.Draft => "Brouillon",
                CompetitionStatus.InProgress => "En cours",
                CompetitionStatus.Finished => "Terminée",
                _ => status.ToString()
            };
        }

        public string FormatVisibility(CompetitionVisibility visibility)
        {
            return visibility switch
            {
                CompetitionVisibility.Private => "Privée",
                CompetitionVisibility.Club => "Club",
                CompetitionVisibility.Public => "Publique",
                _ => visibility.ToString()
            };
        }
    }
}
