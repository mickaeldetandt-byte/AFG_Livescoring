using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class RoundScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public RoundScoreModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int RoundId { get; set; }

        public Round? Round { get; set; }

        [BindProperty]
        public List<int> HoleScores { get; set; } = new();

        public List<int> HolePars { get; set; } = new();
        public int TotalCoursePar { get; set; }

        public IActionResult OnGet()
        {
            Round = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .FirstOrDefault(r => r.Id == RoundId);

            if (Round == null)
                return RedirectToPage("/Competitions");

            // 🔒 Étape 6 : Interdire la saisie individuelle en compétition
            var competition = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == Round.CompetitionId);
            if (competition == null)
                return RedirectToPage("/Competitions");

            if (competition.ScoringMode == ScoringMode.SquadOnly)
            {
                TempData["Error"] = "En compétition, la saisie individuelle est désactivée. Utilisez le scoring par squad.";
                if (Round.SquadId.HasValue)
                {
                    return RedirectToPage("/Squads/Score", new
                    {
                        competitionId = Round.CompetitionId,
                        squadId = Round.SquadId.Value,
                        hole = 1
                    });
                }
                return RedirectToPage("/Leaderboard", new { competitionId = Round.CompetitionId });
            }

            // Charger scores existants
            var existingScores = _db.Scores
                .Where(s => s.RoundId == RoundId)
                .OrderBy(s => s.HoleNumber)
                .ToList();

            HoleScores = new List<int>();
            for (int i = 1; i <= 18; i++)
            {
                var score = existingScores.FirstOrDefault(s => s.HoleNumber == i);
                HoleScores.Add(score?.Strokes ?? 0);
            }

            // Charger les Par
            HolePars = new List<int>();
            for (int i = 0; i < 18; i++) HolePars.Add(0);

            var courseId = competition.CourseId;
            if (courseId != null)
            {
                var holes = _db.Holes.AsNoTracking()
                    .Where(h => h.CourseId == courseId.Value)
                    .ToList();

                foreach (var h in holes)
                {
                    if (h.HoleNumber >= 1 && h.HoleNumber <= 18)
                        HolePars[h.HoleNumber - 1] = h.Par;
                }

                TotalCoursePar = HolePars.Sum();
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            // Recharger le round pour appliquer la règle (ne jamais faire confiance au client)
            var round = _db.Rounds.AsNoTracking().FirstOrDefault(r => r.Id == RoundId);
            if (round == null)
                return RedirectToPage("/Competitions");

            var competition = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == round.CompetitionId);
            if (competition == null)
                return RedirectToPage("/Competitions");

            // 🔒 Étape 6 : Interdire la saisie individuelle en compétition
            if (competition.ScoringMode == ScoringMode.SquadOnly)
            {
                TempData["Error"] = "En compétition, la saisie individuelle est désactivée. Utilisez le scoring par squad.";
                if (round.SquadId.HasValue)
                {
                    return RedirectToPage("/Squads/Score", new
                    {
                        competitionId = round.CompetitionId,
                        squadId = round.SquadId.Value,
                        hole = 1
                    });
                }
                return RedirectToPage("/Leaderboard", new { competitionId = round.CompetitionId });
            }

            // Saisie individuelle autorisée (entraînement)
            for (int i = 0; i < 18; i++)
            {
                int holeNumber = i + 1;

                int strokes = HoleScores[i];

                // Clamp serveur 0..10
                if (strokes < 0) strokes = 0;
                if (strokes > 10) strokes = 10;

                var existing = _db.Scores
                    .FirstOrDefault(s => s.RoundId == RoundId && s.HoleNumber == holeNumber);

                // 0 = non joué => on supprime
                if (strokes == 0)
                {
                    if (existing != null)
                        _db.Scores.Remove(existing);

                    continue;
                }

                if (existing == null)
                {
                    _db.Scores.Add(new Score
                    {
                        RoundId = RoundId,
                        HoleNumber = holeNumber,
                        Strokes = strokes
                    });
                }
                else
                {
                    existing.Strokes = strokes;
                }
            }

            _db.SaveChanges();

            return RedirectToPage(new { roundId = RoundId });
        }
    }
}