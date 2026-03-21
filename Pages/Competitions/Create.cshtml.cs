using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        public CreateModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public Competition Competition { get; set; } = new();

        public void OnGet()
        {
            Competition.ScoringMode = ScoringMode.SquadOnly;
            Competition.Mode = "Competition";
            Competition.CompetitionType = CompetitionType.IndividualStrokePlay;
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
                return Page();

            if (string.IsNullOrWhiteSpace(Competition.Mode))
                Competition.Mode = "Competition";

            if (Competition.Mode != "Competition" && Competition.Mode != "Training")
                Competition.Mode = "Competition";

            if (!Enum.IsDefined(typeof(CompetitionType), Competition.CompetitionType))
                Competition.CompetitionType = CompetitionType.IndividualStrokePlay;

            if (Competition.Mode == "Training")
            {
                Competition.ScoringMode = ScoringMode.IndividualAllowed;
            }
            else
            {
                Competition.ScoringMode = ScoringMode.SquadOnly;
            }

            _db.Competitions.Add(Competition);
            _db.SaveChanges();

            return RedirectToPage("/Competitions");
        }
    }
}