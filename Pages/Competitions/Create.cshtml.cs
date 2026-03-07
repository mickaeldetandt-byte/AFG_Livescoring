using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
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
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
                return Page();

            _db.Competitions.Add(Competition);
            _db.SaveChanges();

            return RedirectToPage("/Competitions");
        }
    }
}