using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;

        public DetailsModel(AppDbContext db)
        {
            _db = db;
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

        public IActionResult OnGet()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            return Page();
        }

        public IActionResult OnPostStart(int id)
        {
            var competition = _db.Competitions.FirstOrDefault(c => c.Id == id);
            if (competition == null)
            {
                TempData["Error"] = "Compétition introuvable.";
                return RedirectToPage("/Competitions");
            }

            if (!CanManageCompetition(competition))
                return Forbid();

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Error"] = "Cette compétition est déjà terminée.";
                return RedirectToPage(new { id });
            }

            var hasPlayers = _db.Rounds.Any(r => r.CompetitionId == id)
                || _db.TeamRounds.Any(tr => tr.CompetitionId == id)
                || _db.MatchPlayRounds.Any(m => m.CompetitionId == id);

            if (!hasPlayers)
            {
                TempData["Error"] = "Impossible de démarrer : aucun participant n'est encore affecté à cette compétition.";
                return RedirectToPage(new { id });
            }

            competition.Status = CompetitionStatus.InProgress;
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Compétition démarrée.";
            return RedirectToPage(new { id });
        }

        public IActionResult OnPostFinish(int id)
        {
            var competition = _db.Competitions.FirstOrDefault(c => c.Id == id);
            if (competition == null)
            {
                TempData["Error"] = "Compétition introuvable.";
                return RedirectToPage("/Competitions");
            }

            if (!CanManageCompetition(competition))
                return Forbid();

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Info"] = "La compétition est déjà terminée.";
                return RedirectToPage(new { id });
            }

            competition.Status = CompetitionStatus.Finished;
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Compétition terminée avec succès.";
            return RedirectToPage(new { id });
        }

        private bool LoadPageData()
        {
            Competition = _db.Competitions
                .Include(c => c.Course)
                .Include(c => c.Club)
                .Include(c => c.CreatedByUser)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == Id);

            if (Competition == null)
                return false;

            if (!CanManageCompetition(Competition))
                throw new UnauthorizedAccessException();

            var rounds = _db.Rounds
                .AsNoTracking()
                .Where(r => r.CompetitionId == Id)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = roundIds.Any()
                ? _db.Scores
                    .AsNoTracking()
                    .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                    .ToList()
                : new List<Score>();

            var squads = _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == Id)
                .ToList();

            _ = _db.TeamRounds.AsNoTracking().Count(tr => tr.CompetitionId == Id);
            _ = _db.MatchPlayRounds.AsNoTracking().Count(m => m.CompetitionId == Id);

            PlayerCount = rounds.Count;
            SquadCount = squads.Count;

            HasStarted = scores.Any()
                         || _db.TeamScores.AsNoTracking()
                             .Join(_db.TeamRounds.AsNoTracking().Where(tr => tr.CompetitionId == Id),
                                   s => s.TeamRoundId,
                                   tr => tr.Id,
                                   (s, tr) => s)
                             .Any(ts => ts.Strokes > 0)
                         || _db.MatchPlayHoleResults.AsNoTracking()
                             .Join(_db.MatchPlayRounds.AsNoTracking().Where(m => m.CompetitionId == Id),
                                   h => h.MatchPlayRoundId,
                                   m => m.Id,
                                   (h, m) => h)
                             .Any();

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

        private bool CanManageCompetition(Competition competition)
        {
            if (User.Identity?.IsAuthenticated != true)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            var currentUser = _db.AppUsers.FirstOrDefault(u => u.Email == email);
            if (currentUser == null)
                return false;

            if (string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
            {
                if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                    return true;

                if (competition.CreatedByUserId.HasValue && currentUser.Id == competition.CreatedByUserId.Value)
                    return true;
            }

            return false;
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