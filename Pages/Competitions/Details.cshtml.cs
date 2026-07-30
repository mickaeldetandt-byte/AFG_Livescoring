using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ICompetitionAuthorizationService _authorizationService;

        public DetailsModel(
            AppDbContext db,
            ICompetitionAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Competition? Competition { get; set; }
        public Course? Course => Competition?.Course;

        public int PlayerCount { get; set; }
        public int SquadCount { get; set; }
        public bool HasStarted { get; set; }
        public int CompletedRounds { get; set; }
        public bool AutoFinishedByScores { get; set; }

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
            var authorizationFailure = await GetAuthorizationFailureAsync(Id);
            if (authorizationFailure != null)
                return authorizationFailure;

            if (!await LoadPageDataAsync())
                return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostStartAsync(int id)
        {
            var authorizationFailure = await GetAuthorizationFailureAsync(id);
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
                TempData["Error"] =
                    "Seule une compétition en brouillon peut être démarrée.";
                return RedirectToPage(new { id });
            }

            if (!await HasMinimumStartStructureAsync(competition))
            {
                TempData["Error"] =
                    "Impossible de démarrer : la structure et les participants de la compétition sont incomplets.";
                return RedirectToPage(new { id });
            }

            competition.Status = CompetitionStatus.InProgress;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            TempData["SuccessMessage"] = "Compétition démarrée.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostFinishAsync(int id)
        {
            var authorizationFailure = await GetAuthorizationFailureAsync(id);
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
                TempData["Error"] =
                    "Seule une compétition en cours peut être terminée.";
                return RedirectToPage(new { id });
            }

            competition.Status = CompetitionStatus.Finished;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            TempData["SuccessMessage"] = "Compétition terminée avec succès.";
            return RedirectToPage(new { id });
        }

        private async Task<IActionResult?> GetAuthorizationFailureAsync(int competitionId)
        {
            var canManageCompetition = await _authorizationService
                .CanManageCompetitionAsync(
                    User,
                    competitionId,
                    HttpContext.RequestAborted);

            if (canManageCompetition)
                return null;

            var competitionExists = await _db.Competitions
                .AsNoTracking()
                .AnyAsync(
                    competition => competition.Id == competitionId,
                    HttpContext.RequestAborted);

            return competitionExists ? Forbid() : NotFound();
        }

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

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Where(r => r.CompetitionId == Id)
                .ToListAsync(HttpContext.RequestAborted);

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = roundIds.Any()
                ? await _db.Scores
                    .AsNoTracking()
                    .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                    .ToListAsync(HttpContext.RequestAborted)
                : new List<Score>();

            var squads = await _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == Id)
                .ToListAsync(HttpContext.RequestAborted);

            PlayerCount = rounds.Count;
            SquadCount = squads.Count;

            HasStarted = scores.Any()
                         || await _db.TeamScores.AsNoTracking()
                             .Join(_db.TeamRounds.AsNoTracking().Where(tr => tr.CompetitionId == Id),
                                   s => s.TeamRoundId,
                                   tr => tr.Id,
                                   (s, tr) => s)
                             .AnyAsync(
                                 ts => ts.Strokes > 0,
                                 HttpContext.RequestAborted)
                         || await _db.MatchPlayHoleResults.AsNoTracking()
                             .Join(_db.MatchPlayRounds.AsNoTracking().Where(m => m.CompetitionId == Id),
                                   h => h.MatchPlayRoundId,
                                   m => m.Id,
                                   (h, m) => h)
                             .AnyAsync(HttpContext.RequestAborted);

            var holesPlayedByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            CompletedRounds = 0;
            foreach (var round in rounds)
            {
                int holesPlayed = holesPlayedByRoundId.TryGetValue(round.Id, out var hp) ? hp : 0;
                if (holesPlayed >= 18)
                    CompletedRounds++;
            }

            AutoFinishedByScores = PlayerCount > 0 && CompletedRounds == PlayerCount;

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
