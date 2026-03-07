using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages.Squads
{
    public class ManageModel : PageModel
    {
        private readonly AppDbContext _db;

        public ManageModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int competitionId { get; set; }

        public int CompetitionId => competitionId;

        public string CompetitionName { get; set; } = "";

        public bool HasScores { get; set; }

        // ✅ NEW : Entraînement ?
        public bool IsTraining { get; set; }

        // ✅ Limites dynamiques affichées dans la vue
        public int MinSquadSize { get; set; }
        public int MaxSquadSize { get; set; }

        public List<SquadView> Squads { get; set; } = new();

        // ✅ NEW : joueurs non affectés à un squad
        public List<Round> UnassignedRounds { get; set; } = new();

        public class SquadView
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int StartHole { get; set; }
            public List<Round> Rounds { get; set; } = new();
        }

        // =============================
        // GET
        // =============================
        public IActionResult OnGet()
        {
            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            CompetitionName = comp.Name;

            // Mode
            IsTraining = (comp.ScoringMode == ScoringMode.IndividualAllowed);

            // limites
            var (min, max) = SquadRules.GetLimits(comp);
            MinSquadSize = min;
            MaxSquadSize = max;

            HasScores = CompetitionHasScores(competitionId);

            LoadSquadsAndUnassigned();

            return Page();
        }

        // =============================
        // GENERATE (aléatoire)
        // =============================
        public IActionResult OnPostGenerate(int competitionId, int squadSize)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            CompetitionName = comp.Name;
            IsTraining = (comp.ScoringMode == ScoringMode.IndividualAllowed);

            var (minAllowed, maxAllowed) = SquadRules.GetLimits(comp);
            MinSquadSize = minAllowed;
            MaxSquadSize = maxAllowed;

            // bornage
            if (squadSize < minAllowed) squadSize = minAllowed;
            if (squadSize > maxAllowed) squadSize = maxAllowed;

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible de générer les squads : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            var rounds = _db.Rounds
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == competitionId)
                .ToList();

            if (!rounds.Any())
            {
                TempData["Message"] = "Aucun round/joueur dans cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (_db.Squads.Any(s => s.CompetitionId == competitionId))
            {
                TempData["Message"] = "Des squads existent déjà. Clique sur Réinitialiser avant de regénérer.";
                return RedirectToPage(new { competitionId });
            }

            var rnd = new Random();
            rounds = rounds.OrderBy(_ => rnd.Next()).ToList();

            int squadIndex = 1;
            int startHole = 1;

            int n = rounds.Count;
            int maxSize = squadSize;
            int minSize = minAllowed;

            if (n < minSize)
            {
                TempData["Message"] = $"Impossible : {n} joueur(s). Minimum requis = {minSize} (selon le mode).";
                return RedirectToPage(new { competitionId });
            }

            int minSquads = (int)Math.Ceiling(n / (double)maxSize);
            int maxSquads = n / minSize;

            if (minSquads > maxSquads)
            {
                TempData["Message"] = $"Impossible : {n} joueurs ne peuvent pas être répartis en squads de {minSize} à {maxSize}.";
                return RedirectToPage(new { competitionId });
            }

            int k = minSquads;

            int baseSize = n / k;
            int extra = n % k;

            var sizes = new List<int>(k);
            for (int idx = 0; idx < k; idx++)
            {
                int size = baseSize + (idx < extra ? 1 : 0);
                sizes.Add(size);
            }

            if (sizes.Any(s => s < minSize || s > maxSize))
            {
                TempData["Message"] = $"Répartition invalide : {string.Join("-", sizes)} (attendu entre {minSize} et {maxSize}).";
                return RedirectToPage(new { competitionId });
            }

            int i = 0;
            foreach (var take in sizes)
            {
                var chunk = rounds.Skip(i).Take(take).ToList();

                var squad = new Squad
                {
                    CompetitionId = competitionId,
                    Name = $"Squad {squadIndex}",
                    StartHole = startHole
                };

                _db.Squads.Add(squad);
                _db.SaveChanges();

                foreach (var r in chunk)
                    r.SquadId = squad.Id;

                _db.SaveChanges();

                squadIndex++;

                startHole++;
                if (startHole > 18) startHole = 18;

                i += take;
            }

            TempData["Message"] = $"Squads générés : {squadIndex - 1}. (Mode: {comp.ScoringMode}, tailles: {minSize} à {maxSize})";
            return RedirectToPage(new { competitionId });
        }

        // =============================
        // ✅ NEW : CREATE SQUAD (manuel, entraînement)
        // =============================
        public IActionResult OnPostCreateSquad(int competitionId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null) return RedirectToPage("/Competitions");

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (comp.ScoringMode != ScoringMode.IndividualAllowed)
            {
                TempData["Message"] = "Création manuelle disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            // Nom auto : Squad 1, Squad 2...
            int nextIndex = _db.Squads.Count(s => s.CompetitionId == competitionId) + 1;

            var squad = new Squad
            {
                CompetitionId = competitionId,
                Name = $"Squad {nextIndex}",
                StartHole = 1 // entraînement : pas critique
            };

            _db.Squads.Add(squad);
            _db.SaveChanges();

            TempData["Message"] = $"Squad créé : {squad.Name}";
            return RedirectToPage(new { competitionId });
        }

        // =============================
        // ✅ NEW : ASSIGN (manuel, entraînement)
        // =============================
        public IActionResult OnPostAssign(int competitionId, int roundId, int squadId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null) return RedirectToPage("/Competitions");

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (comp.ScoringMode != ScoringMode.IndividualAllowed)
            {
                TempData["Message"] = "Affectation manuelle disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            var round = _db.Rounds.Include(r => r.Player).FirstOrDefault(r => r.Id == roundId);
            if (round == null || round.CompetitionId != competitionId)
            {
                TempData["Message"] = "Round introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            var squad = _db.Squads.FirstOrDefault(s => s.Id == squadId);
            if (squad == null || squad.CompetitionId != competitionId)
            {
                TempData["Message"] = "Squad introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            var (minAllowed, maxAllowed) = SquadRules.GetLimits(comp);

            int currentCount = _db.Rounds.Count(r => r.CompetitionId == competitionId && r.SquadId == squadId);
            if (currentCount >= maxAllowed)
            {
                TempData["Message"] = $"Squad plein : maximum {maxAllowed} joueur(s) en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            round.SquadId = squadId;
            _db.SaveChanges();

            TempData["Message"] = $"{round.Player?.FirstName} {round.Player?.LastName} affecté à {squad.Name}.";
            return RedirectToPage(new { competitionId });
        }

        // =============================
        // ✅ NEW : REMOVE (manuel, entraînement)
        // =============================
        public IActionResult OnPostRemove(int competitionId, int roundId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null) return RedirectToPage("/Competitions");

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (comp.ScoringMode != ScoringMode.IndividualAllowed)
            {
                TempData["Message"] = "Retrait manuel disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            var round = _db.Rounds.Include(r => r.Player).FirstOrDefault(r => r.Id == roundId);
            if (round == null || round.CompetitionId != competitionId)
            {
                TempData["Message"] = "Round introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            round.SquadId = null;
            _db.SaveChanges();

            TempData["Message"] = $"{round.Player?.FirstName} {round.Player?.LastName} retiré du squad.";
            return RedirectToPage(new { competitionId });
        }

        // =============================
        // CLEAR
        // =============================
        public IActionResult OnPostClear(int competitionId)
        {
            this.competitionId = competitionId;

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible de réinitialiser : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            var squads = _db.Squads
                .Where(s => s.CompetitionId == competitionId)
                .ToList();

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == competitionId && r.SquadId != null)
                .ToList();

            foreach (var r in rounds)
                r.SquadId = null;

            _db.SaveChanges();

            if (squads.Any())
            {
                _db.Squads.RemoveRange(squads);
                _db.SaveChanges();
            }

            TempData["Message"] = "Squads réinitialisés.";
            return RedirectToPage(new { competitionId });
        }

        // =============================
        // HELPERS
        // =============================
        private bool CompetitionHasScores(int compId)
        {
            return _db.Scores
                .Include(s => s.Round)
                .Any(s => s.Round.CompetitionId == compId && s.Strokes > 0);
        }

        private void LoadSquadsAndUnassigned()
        {
            var squads = _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == competitionId)
                .OrderBy(s => s.Id)
                .ToList();

            var rounds = _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == competitionId)
                .ToList();

            Squads = squads.Select(s => new SquadView
            {
                Id = s.Id,
                Name = s.Name,
                StartHole = s.StartHole,
                Rounds = rounds.Where(r => r.SquadId == s.Id).ToList()
            }).ToList();

            UnassignedRounds = rounds
                .Where(r => r.SquadId == null)
                .OrderBy(r => r.Player!.LastName)
                .ThenBy(r => r.Player!.FirstName)
                .ToList();

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            CompetitionName = comp?.Name ?? "";

            if (comp != null)
            {
                IsTraining = (comp.ScoringMode == ScoringMode.IndividualAllowed);
                var (min, max) = SquadRules.GetLimits(comp);
                MinSquadSize = min;
                MaxSquadSize = max;
            }
        }
    }
}