using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    [Authorize]
    public class CompetitionParticipantsModel : PageModel
    {
        private readonly AppDbContext _db;

        public CompetitionParticipantsModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        public Competition? Competition { get; set; }

        public bool IsTraining { get; set; }
        public bool HasStarted { get; set; }
        public bool HasSquads { get; set; }
        public bool CanEditParticipants { get; set; }
        public string LockMessage { get; set; } = "";

        public List<Player> Players { get; set; } = new();
        public List<Round> Rounds { get; set; } = new();

        [BindProperty]
        public int SelectedPlayerId { get; set; }

        public IActionResult OnGet()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            return Page();
        }

        public IActionResult OnPostAdd()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!CanEditParticipants)
            {
                TempData["Error"] = LockMessage;
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            if (SelectedPlayerId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Sélectionne un joueur.");
                return Page();
            }

            bool exists = _db.Rounds.Any(r => r.CompetitionId == CompetitionId && r.PlayerId == SelectedPlayerId);
            if (!exists)
            {
                _db.Rounds.Add(new Round
                {
                    CompetitionId = CompetitionId,
                    PlayerId = SelectedPlayerId
                });

                _db.SaveChanges();
            }

            return RedirectToPage(new { competitionId = CompetitionId });
        }

        public IActionResult OnPostRemove(int roundId)
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!CanEditParticipants)
            {
                TempData["Error"] = LockMessage;
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            var round = _db.Rounds.FirstOrDefault(r => r.Id == roundId && r.CompetitionId == CompetitionId);
            if (round != null)
            {
                _db.Rounds.Remove(round);
                _db.SaveChanges();
            }

            return RedirectToPage(new { competitionId = CompetitionId });
        }

        private bool LoadPageData()
        {
            Competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return false;

            IsTraining = Competition.ScoringMode == ScoringMode.IndividualAllowed;

            Players = _db.Players
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();

            Rounds = _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == CompetitionId)
                .OrderBy(r => r.Player!.LastName)
                .ThenBy(r => r.Player!.FirstName)
                .ToList();

            var roundIds = Rounds.Select(r => r.Id).ToList();

            HasStarted = false;
            if (roundIds.Any())
            {
                HasStarted = _db.Scores
                    .AsNoTracking()
                    .Any(s => roundIds.Contains(s.RoundId) && s.Strokes > 0);
            }

            HasSquads = _db.Squads
                .AsNoTracking()
                .Any(s => s.CompetitionId == CompetitionId);

            CanEditParticipants = IsTraining || (!HasStarted && !HasSquads);

            if (CanEditParticipants)
            {
                LockMessage = "";
            }
            else if (!IsTraining && HasStarted)
            {
                LockMessage = "Participants verrouillés : la compétition a déjà démarré.";
            }
            else if (HasSquads)
            {
                LockMessage = "Participants verrouillés : des squads existent déjà.";
            }
            else
            {
                LockMessage = "Modification impossible.";
            }

            return true;
        }
    }
}