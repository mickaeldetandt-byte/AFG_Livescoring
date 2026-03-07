using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class RoundCardModel : PageModel
    {
        private readonly AppDbContext _db;

        public RoundCardModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int RoundId { get; set; }

        public Round? Round { get; set; }
        public Competition? Competition { get; set; }
        public Squad? Squad { get; set; }

        public int CourseParTotal { get; set; }
        public int HolesPlayed { get; set; }
        public int TotalStrokes { get; set; }
        public int PlayedPar { get; set; }
        public int DiffToPar { get; set; }

        public bool CanBeLocked { get; set; }

        public List<HoleRow> Holes { get; set; } = new();

        public class HoleRow
        {
            public int HoleNumber { get; set; }
            public int? Par { get; set; }
            public int Strokes { get; set; } // 0 = non joué
            public int? DiffToPar { get; set; } // null si non joué
        }

        public IActionResult OnGet()
        {
            Round = _db.Rounds
                .Include(r => r.Player)
                .AsNoTracking()
                .FirstOrDefault(r => r.Id == RoundId);

            if (Round == null)
                return RedirectToPage("/Competitions");

            // Squad
            if (Round.SquadId.HasValue)
            {
                Squad = _db.Squads
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Id == Round.SquadId.Value);
            }

            Competition = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == Round.CompetitionId);

            if (Competition == null || Competition.CourseId == null)
            {
                TempData["Error"] = "Compétition ou parcours introuvable. Impossible d'afficher la carte.";
                return RedirectToPage("/Competitions");
            }

            // ✅ IMPORTANT :
            // En compétition (SquadOnly), la carte reste consultable (lecture seule).
            // L’interdiction de saisie se fait dans RoundScore + dans le scoring squad.

            // Par par trou
            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            CourseParTotal = parByHole.Count > 0 ? parByHole.Values.Sum() : 0;

            // Scores du round (on ignore les 0)
            var scores = _db.Scores
                .AsNoTracking()
                .Where(s => s.RoundId == RoundId && s.Strokes > 0)
                .ToList();

            // ✅ Robustesse : si doublons, on garde le premier
            var strokesByHole = scores
                .GroupBy(s => s.HoleNumber)
                .ToDictionary(g => g.Key, g => g.First().Strokes);

            // Construire trous 1..18
            Holes = new List<HoleRow>();
            for (int hole = 1; hole <= 18; hole++)
            {
                parByHole.TryGetValue(hole, out int par);
                int strokes = strokesByHole.TryGetValue(hole, out int st) ? st : 0;

                int? diff = null;
                if (strokes > 0 && parByHole.ContainsKey(hole))
                    diff = strokes - par;

                Holes.Add(new HoleRow
                {
                    HoleNumber = hole,
                    Par = parByHole.ContainsKey(hole) ? par : (int?)null,
                    Strokes = strokes,
                    DiffToPar = diff
                });
            }

            // Totaux
            var played = Holes.Where(h => h.Strokes > 0).ToList();
            HolesPlayed = played.Count;
            CanBeLocked = (HolesPlayed == 18);

            TotalStrokes = played.Sum(h => h.Strokes);
            PlayedPar = played.Sum(h => h.Par ?? 0);
            DiffToPar = (HolesPlayed == 0) ? 0 : (TotalStrokes - PlayedPar);

            return Page();
        }

        public IActionResult OnPostLock(int roundId)
        {
            var round = _db.Rounds.FirstOrDefault(r => r.Id == roundId);
            if (round == null)
            {
                TempData["Error"] = "Round introuvable.";
                return RedirectToPage("/Competitions");
            }

            var competition = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == round.CompetitionId);
            if (competition == null)
            {
                TempData["Error"] = "Compétition introuvable.";
                return RedirectToPage("/Competitions");
            }

            // 🔒 En compétition : pas de lock via la carte individuelle
            if (competition.ScoringMode == ScoringMode.SquadOnly)
            {
                TempData["Error"] = "En compétition, le verrouillage se fait via le scoring squad (fin de partie).";
                return RedirectToPage("/RoundCard", new { roundId });
            }

            // En entraînement : tu peux garder le lock si tu veux (optionnel)
            if (!round.SquadId.HasValue)
            {
                TempData["Error"] = "Ce joueur n'est pas affecté à un squad. Verrouillage impossible.";
                return RedirectToPage(new { roundId });
            }

            int competitionId = round.CompetitionId;
            int squadId = round.SquadId.Value;

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == competitionId && r.SquadId == squadId)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var counts = _db.Scores
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .GroupBy(s => s.RoundId)
                .Select(g => new { RoundId = g.Key, Count = g.Select(x => x.HoleNumber).Distinct().Count() })
                .ToDictionary(x => x.RoundId, x => x.Count);

            var notComplete = rounds
                .Where(r => !counts.ContainsKey(r.Id) || counts[r.Id] != 18)
                .ToList();

            if (notComplete.Any())
            {
                TempData["Error"] = $"Verrouillage impossible : {notComplete.Count} carte(s) incomplète(s) dans le squad.";
                return RedirectToPage(new { roundId });
            }

            foreach (var r in rounds)
                r.IsLocked = true;

            _db.SaveChanges();

            TempData["Info"] = "Squad verrouillé définitivement (toutes les cartes).";
            return RedirectToPage(new { roundId });
        }
    }
}