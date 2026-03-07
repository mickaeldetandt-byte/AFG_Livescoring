using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
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

        public List<Player> Players { get; set; } = new();
        public List<Round> Rounds { get; set; } = new();

        [BindProperty]
        public int SelectedPlayerId { get; set; }

        public IActionResult OnGet()
        {
            Competition = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == CompetitionId);
            if (Competition == null) return RedirectToPage("/Competitions");

            IsTraining = (Competition.ScoringMode == ScoringMode.IndividualAllowed);

            Players = _db.Players.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToList();

            Rounds = _db.Rounds.AsNoTracking()
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == CompetitionId)
                .OrderBy(r => r.Player!.LastName).ThenBy(r => r.Player!.FirstName)
                .ToList();

            return Page();
        }

        public IActionResult OnPostAdd()
        {
            if (SelectedPlayerId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Sélectionne un joueur.");
                return OnGet();
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
            var round = _db.Rounds.FirstOrDefault(r => r.Id == roundId);
            if (round != null)
            {
                int compId = round.CompetitionId;
                _db.Rounds.Remove(round);
                _db.SaveChanges();
                return RedirectToPage(new { competitionId = compId });
            }

            return RedirectToPage("/Competitions");
        }
    }
}