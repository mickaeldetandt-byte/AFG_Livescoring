using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Squads
{
    public class ScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public ScoreModel(AppDbContext db)
        {
            _db = db;
        }

        // -------------------------
        // QueryString (GET)
        // -------------------------
        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SquadId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Hole { get; set; }

        // Mode correction (organisateur)
        [BindProperty(SupportsGet = true)]
        public bool Edit { get; set; }

        // -------------------------
        // Données affichées
        // -------------------------
        public Competition? Competition { get; set; }
        public Squad? Squad { get; set; }

        [BindProperty]
        public int CurrentHole { get; set; }

        public int? CurrentPar { get; set; }
        public bool IsHoleValidated { get; set; }
        public bool IsLastHole { get; set; }
        public bool IsSquadLocked { get; set; }

        public List<PlayerScoreRow> Players { get; set; } = new();

        public class PlayerScoreRow
        {
            public int RoundId { get; set; }
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public int Strokes { get; set; }
        }

        private bool ComputeIsSquadLocked()
        {
            if (CompetitionId <= 0 || SquadId <= 0) return false;

            return _db.Rounds.AsNoTracking().Any(r =>
                r.CompetitionId == CompetitionId &&
                r.SquadId == SquadId &&
                r.IsLocked);
        }

        private bool IsSquadComplete()
        {
            var squadRoundIds = _db.Rounds
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .Select(r => r.Id)
                .ToList();

            if (squadRoundIds.Count == 0) return false;

            var counts = _db.Scores
                .AsNoTracking()
                .Where(s => squadRoundIds.Contains(s.RoundId) && s.Strokes > 0)
                .GroupBy(s => s.RoundId)
                .Select(g => new { RoundId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.RoundId, x => x.Count);

            return squadRoundIds.All(id => counts.ContainsKey(id) && counts[id] == 18);
        }

        private void LockSquad()
        {
            var roundsToLock = _db.Rounds
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .ToList();

            foreach (var r in roundsToLock)
                r.IsLocked = true;

            _db.SaveChanges();
        }

        private void AutoLockSquadIfComplete()
        {
            if (!IsSquadComplete()) return;

            LockSquad();
            TempData["Info"] = "Squad verrouillé automatiquement (18 trous remplis pour tous).";
        }

        // =========================================================
        // GET
        // =========================================================
        public IActionResult OnGet()
        {
            // 1) Competition + Course
            Competition = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            if (Competition.CourseId == null)
            {
                TempData["Error"] = "Aucun parcours n'est associé à cette compétition. Le Live est désactivé.";
                return RedirectToPage("/Competitions");
            }

            // 2) Squad + vérification appartenance compétition
            Squad = _db.Squads
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == SquadId);

            if (Squad == null || Squad.CompetitionId != CompetitionId)
            {
                TempData["Error"] = "Squad introuvable ou ne correspond pas à la compétition.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            // 3) Lock status
            IsSquadLocked = ComputeIsSquadLocked();

            // IMPORTANT :
            // Si le squad est COMPLET mais PAS verrouillé, on le re-verrouille automatiquement
            // SAUF si on est en mode correction (Edit=true)
            if (!IsSquadLocked && !Edit && IsSquadComplete())
            {
                LockSquad();
                IsSquadLocked = true;
                TempData["Info"] = "Squad re-verrouillé automatiquement (aucune correction en cours).";
            }

            // 4) Trou courant
            CurrentHole = Hole ?? Squad.StartHole;
            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            IsLastHole = (CurrentHole == 18);

            // 5) Par du trou courant
            CurrentPar = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value && h.HoleNumber == CurrentHole)
                .Select(h => (int?)h.Par)
                .FirstOrDefault();

            // 6) Rounds du squad
            var rounds = _db.Rounds
                .Include(r => r.Player)
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .ToList();

            if (rounds.Count == 0)
            {
                TempData["Error"] = "Aucun joueur n'est affecté à ce squad.";
                return RedirectToPage("/Squads/Manage", new { competitionId = CompetitionId });
            }

            var roundIds = rounds.Select(r => r.Id).ToList();

            // 7) Scores existants du trou courant
            var existingScores = _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.HoleNumber == CurrentHole)
                .ToList();

            IsHoleValidated = existingScores.Any();

            var scoreByRoundId = existingScores
                .GroupBy(s => s.RoundId)
                .ToDictionary(g => g.Key, g => g.First().Strokes);

            Players = rounds
                .OrderBy(r => r.Player!.LastName)
                .ThenBy(r => r.Player!.FirstName)
                .Select(r => new PlayerScoreRow
                {
                    RoundId = r.Id,
                    PlayerId = r.PlayerId,
                    PlayerName = r.Player != null
                        ? (r.Player.FirstName + " " + r.Player.LastName).Trim()
                        : ("PlayerId=" + r.PlayerId),
                    Strokes = scoreByRoundId.TryGetValue(r.Id, out var strokes) ? strokes : 0
                })
                .ToList();

            return Page();
        }

        // =========================================================
        // POST : Valider le trou
        // =========================================================
        public IActionResult OnPost([FromForm] Dictionary<int, int> Scores)
        {
            // recalcul lock
            IsSquadLocked = ComputeIsSquadLocked();
            if (IsSquadLocked)
            {
                TempData["Error"] = "Squad verrouillé. Modification impossible.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole });
            }

            var squad = _db.Squads.AsNoTracking().FirstOrDefault(s => s.Id == SquadId);
            if (squad == null || squad.CompetitionId != CompetitionId)
            {
                TempData["Error"] = "Squad introuvable ou ne correspond pas à la compétition.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            if (Scores == null || Scores.Count == 0)
            {
                TempData["Error"] = "Aucun score reçu.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
            }

            if (Scores.Values.Any(v => v <= 0))
            {
                TempData["Error"] = "Tous les joueurs doivent avoir un score supérieur à 0.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
            }

            var allowedRoundIds = _db.Rounds.AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .Select(r => r.Id)
                .ToHashSet();

            var postedRoundIds = Scores.Keys.ToList();
            if (postedRoundIds.Any(id => !allowedRoundIds.Contains(id)))
            {
                TempData["Error"] = "Données invalides (round non autorisé).";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            bool alreadyValidated = _db.Scores.Any(s => postedRoundIds.Contains(s.RoundId) && s.HoleNumber == CurrentHole);
            if (alreadyValidated)
            {
                TempData["Error"] = "Ce trou a déjà été validé.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
            }

            foreach (var kvp in Scores)
            {
                _db.Scores.Add(new Score
                {
                    RoundId = kvp.Key,
                    HoleNumber = CurrentHole,
                    Strokes = kvp.Value
                });
            }
            _db.SaveChanges();

            // AUTO-LOCK : re-lock automatique si le squad est complet
            AutoLockSquadIfComplete();

            // Navigation
            if (CurrentHole >= 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole + 1, edit = Edit });
        }

        // =========================================================
        // POST : Corriger le trou
        // =========================================================
        public IActionResult OnPostCorrect()
        {
            IsSquadLocked = ComputeIsSquadLocked();
            if (IsSquadLocked)
            {
                TempData["Error"] = "Squad verrouillé. Modification impossible.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
            }

            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            var roundIds = _db.Rounds.AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .Select(r => r.Id)
                .ToList();

            if (roundIds.Count == 0)
            {
                TempData["Error"] = "Aucun joueur n'est affecté à ce squad.";
                return RedirectToPage("/Squads/Manage", new { competitionId = CompetitionId });
            }

            var toDelete = _db.Scores
                .Where(s => roundIds.Contains(s.RoundId) && s.HoleNumber == CurrentHole)
                .ToList();

            if (toDelete.Count == 0)
            {
                TempData["Error"] = "Aucun score à corriger sur ce trou.";
                return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
            }

            _db.Scores.RemoveRange(toDelete);
            _db.SaveChanges();

            TempData["Info"] = $"Trou {CurrentHole} réouvert (scores supprimés).";
            return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = Edit });
        }

        // =========================================================
        // POST : Déverrouiller (organisateur)
        // =========================================================
        public IActionResult OnPostUnlock(int competitionId, int squadId, int hole)
        {
            CompetitionId = competitionId;
            SquadId = squadId;
            CurrentHole = hole;

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .ToList();

            if (rounds.Count == 0)
            {
                TempData["Error"] = "Aucun round trouvé pour ce squad.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            foreach (var r in rounds)
                r.IsLocked = false;

            _db.SaveChanges();

            TempData["Info"] = "Squad déverrouillé (mode correction).";
            // IMPORTANT : on arrive en mode correction pour éviter le re-lock immédiat
            return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = CurrentHole, edit = true });
        }

        // Sécurité : si quelqu'un appelle Unlock en GET
        public IActionResult OnGetUnlock()
        {
            TempData["Error"] = "Déverrouillage uniquement en POST.";
            return RedirectToPage("/Squads/Score", new { competitionId = CompetitionId, squadId = SquadId, hole = Hole });
        }
    }
}