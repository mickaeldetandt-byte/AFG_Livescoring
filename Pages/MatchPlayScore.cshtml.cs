using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages
{
    [Authorize(Roles = "Admin,Club")]
    public class MatchPlayScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public MatchPlayScoreModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int matchId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? EditHole { get; set; }

        [BindProperty]
        public int TeamAScore { get; set; }

        [BindProperty]
        public int TeamBScore { get; set; }

        [BindProperty]
        public int TeamAPlayer1Score { get; set; }

        [BindProperty]
        public int TeamAPlayer2Score { get; set; }

        [BindProperty]
        public int TeamBPlayer1Score { get; set; }

        [BindProperty]
        public int TeamBPlayer2Score { get; set; }

        public MatchPlayRound? Match { get; set; }
        public string CompetitionName { get; set; } = "";
        public string TeamAName { get; set; } = "";
        public string TeamBName { get; set; } = "";

        public string TeamAPlayer1Name { get; set; } = "Joueur 1";
        public string TeamAPlayer2Name { get; set; } = "Joueur 2";
        public string TeamBPlayer1Name { get; set; } = "Joueur 1";
        public string TeamBPlayer2Name { get; set; } = "Joueur 2";

        public List<MatchPlayHoleResult> HoleResults { get; set; } = new();

        public bool IsCompetitionFinished { get; set; }

        public bool IsFourballMatchPlay =>
            Match?.Competition?.CompetitionType == CompetitionType.MatchPlayFourball;

        public bool IsTeamMatchPlay =>
            Match?.Competition?.CompetitionType == CompetitionType.MatchPlayScramble
            || Match?.Competition?.CompetitionType == CompetitionType.MatchPlayFoursome
            || Match?.Competition?.CompetitionType == CompetitionType.MatchPlayFourball;

        public int DisplayHole => EditHole ?? Match?.CurrentHole ?? 1;

        private IActionResult RedirectFinishedCompetition()
        {
            TempData["Message"] = "La compétition est terminée. Le scoring est verrouillé.";
            return RedirectToPage("/Competitions/Details", new { id = Match?.CompetitionId });
        }

        public IActionResult OnGet()
        {
            if (!LoadData())
                return RedirectToPage("/Competitions");

            if (Match == null)
                return RedirectToPage("/Competitions");

            IsCompetitionFinished = Match.Competition?.Status == CompetitionStatus.Finished;

            if (IsCompetitionFinished)
            {
                TempData["Message"] = "La compétition est terminée. Le scoring est verrouillé.";
            }

            if (EditHole.HasValue)
            {
                if (EditHole.Value < 1 || EditHole.Value > 18)
                {
                    TempData["Message"] = "Trou à corriger invalide.";
                    return RedirectToPage("/MatchPlayScore", new { matchId });
                }

                var existing = HoleResults.FirstOrDefault(h => h.HoleNumber == EditHole.Value);
                if (existing == null)
                {
                    TempData["Message"] = $"Aucun résultat enregistré pour le trou {EditHole.Value}.";
                    return RedirectToPage("/MatchPlayScore", new { matchId });
                }

                TeamAScore = existing.TeamAScore ?? 0;
                TeamBScore = existing.TeamBScore ?? 0;
                TeamAPlayer1Score = existing.TeamAPlayer1Score ?? 0;
                TeamAPlayer2Score = existing.TeamAPlayer2Score ?? 0;
                TeamBPlayer1Score = existing.TeamBPlayer1Score ?? 0;
                TeamBPlayer2Score = existing.TeamBPlayer2Score ?? 0;
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            if (!LoadData())
                return RedirectToPage("/Competitions");

            if (Match == null)
            {
                TempData["Message"] = "Match introuvable.";
                return RedirectToPage("/Competitions");
            }

            if (Match.Competition?.Status == CompetitionStatus.Finished)
                return RedirectFinishedCompetition();

            int targetHole = EditHole ?? Match.CurrentHole;

            if (targetHole < 1 || targetHole > 18)
            {
                TempData["Message"] = "Trou invalide.";
                return RedirectToPage("/MatchPlayScore", new { matchId });
            }

            if (!EditHole.HasValue && Match.CurrentHole > 18)
            {
                TempData["Message"] = "Tous les trous ont déjà été saisis.";
                return RedirectToPage("/MatchPlayScore", new { matchId });
            }

            var existing = _db.MatchPlayHoleResults.FirstOrDefault(h =>
                h.MatchPlayRoundId == matchId &&
                h.HoleNumber == targetHole);

            MatchPlayHoleResult holeResult;

            if (existing == null)
            {
                holeResult = new MatchPlayHoleResult
                {
                    MatchPlayRoundId = matchId,
                    HoleNumber = targetHole
                };
                _db.MatchPlayHoleResults.Add(holeResult);
            }
            else
            {
                holeResult = existing;
            }

            if (IsFourballMatchPlay)
            {
                if (TeamAPlayer1Score <= 0 || TeamAPlayer2Score <= 0 || TeamBPlayer1Score <= 0 || TeamBPlayer2Score <= 0)
                {
                    TempData["Message"] = "Les 4 scores joueurs doivent être supérieurs à 0.";
                    return RedirectToPage("/MatchPlayScore", new { matchId, EditHole });
                }

                int bestA = Math.Min(TeamAPlayer1Score, TeamAPlayer2Score);
                int bestB = Math.Min(TeamBPlayer1Score, TeamBPlayer2Score);

                holeResult.TeamAPlayer1Score = TeamAPlayer1Score;
                holeResult.TeamAPlayer2Score = TeamAPlayer2Score;
                holeResult.TeamBPlayer1Score = TeamBPlayer1Score;
                holeResult.TeamBPlayer2Score = TeamBPlayer2Score;

                holeResult.TeamAScore = bestA;
                holeResult.TeamBScore = bestB;

                if (bestA == bestB)
                {
                    holeResult.IsHalved = true;
                    holeResult.WinnerTeamId = null;
                }
                else if (bestA < bestB)
                {
                    holeResult.IsHalved = false;
                    holeResult.WinnerTeamId = Match.TeamAId;
                }
                else
                {
                    holeResult.IsHalved = false;
                    holeResult.WinnerTeamId = Match.TeamBId;
                }
            }
            else
            {
                if (TeamAScore <= 0 || TeamBScore <= 0)
                {
                    TempData["Message"] = "Les scores doivent être supérieurs à 0.";
                    return RedirectToPage("/MatchPlayScore", new { matchId, EditHole });
                }

                holeResult.TeamAScore = TeamAScore;
                holeResult.TeamBScore = TeamBScore;
                holeResult.IsHalved = TeamAScore == TeamBScore;
                holeResult.WinnerTeamId = TeamAScore == TeamBScore
                    ? null
                    : (TeamAScore < TeamBScore ? Match.TeamAId : Match.TeamBId);
            }

            _db.SaveChanges();

            var allResults = _db.MatchPlayHoleResults
                .Where(h => h.MatchPlayRoundId == matchId)
                .OrderBy(h => h.HoleNumber)
                .ToList();

            var summary = MatchPlayCalculator.Calculate(Match, allResults);

            Match.StatusText = summary.StatusText;
            Match.ResultText = summary.ResultText;
            Match.WinnerTeamId = summary.WinnerTeamId;

            if (!EditHole.HasValue)
            {
                if (targetHole < 18)
                {
                    Match.CurrentHole = targetHole + 1;
                    Match.IsFinished = false;
                }
                else
                {
                    Match.CurrentHole = 18;
                    Match.IsFinished = true;
                }
            }
            else
            {
                if (allResults.Count >= 18)
                {
                    Match.IsFinished = true;
                    Match.CurrentHole = 18;
                }
            }

            _db.SaveChanges();

            TempData["Message"] = existing == null
                ? $"Trou {targetHole} enregistré."
                : $"Trou {targetHole} corrigé.";

            return RedirectToPage("/MatchPlayScore", new { matchId });
        }

        private bool LoadData()
        {
            Match = _db.MatchPlayRounds
                .Include(m => m.Competition)
                .Include(m => m.TeamA)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .Include(m => m.TeamB)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .FirstOrDefault(m => m.Id == matchId);

            if (Match == null)
                return false;

            CompetitionName = Match.Competition?.Name ?? "";

            var teamAPlayers = Match.TeamA?.TeamPlayers?
                .OrderBy(x => x.Order)
                .Select(x => x.Player)
                .Where(p => p != null)
                .ToList() ?? new List<Player?>();

            var teamBPlayers = Match.TeamB?.TeamPlayers?
                .OrderBy(x => x.Order)
                .Select(x => x.Player)
                .Where(p => p != null)
                .ToList() ?? new List<Player?>();

            TeamAName = string.Join(" / ",
                teamAPlayers.Select(p => $"{p!.FirstName} {p.LastName}"));

            TeamBName = string.Join(" / ",
                teamBPlayers.Select(p => $"{p!.FirstName} {p.LastName}"));

            TeamAPlayer1Name = teamAPlayers.Count > 0
                ? $"{teamAPlayers[0]!.FirstName} {teamAPlayers[0]!.LastName}"
                : "Joueur 1";

            TeamAPlayer2Name = teamAPlayers.Count > 1
                ? $"{teamAPlayers[1]!.FirstName} {teamAPlayers[1]!.LastName}"
                : "Joueur 2";

            TeamBPlayer1Name = teamBPlayers.Count > 0
                ? $"{teamBPlayers[0]!.FirstName} {teamBPlayers[0]!.LastName}"
                : "Joueur 1";

            TeamBPlayer2Name = teamBPlayers.Count > 1
                ? $"{teamBPlayers[1]!.FirstName} {teamBPlayers[1]!.LastName}"
                : "Joueur 2";

            HoleResults = _db.MatchPlayHoleResults
                .Where(h => h.MatchPlayRoundId == matchId)
                .OrderBy(h => h.HoleNumber)
                .ToList();

            return true;
        }
    }
}