using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Players
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<Player> Players { get; set; } = new();

        [BindProperty]
        public Player NewPlayer { get; set; } = new();

        public void OnGet()
        {
            LoadPlayers();
        }

        public IActionResult OnPostAdd()
        {
            if (string.IsNullOrWhiteSpace(NewPlayer.FirstName) || string.IsNullOrWhiteSpace(NewPlayer.LastName))
            {
                LoadPlayers();
                ModelState.AddModelError(string.Empty, "Prénom et nom obligatoires.");
                return Page();
            }

            _db.Players.Add(NewPlayer);
            _db.SaveChanges();

            TempData["Success"] = "Joueur ajouté.";
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            var player = _db.Players.Find(id);
            if (player != null)
            {
                _db.Players.Remove(player);
                _db.SaveChanges();
                TempData["Success"] = "Joueur supprimé.";
            }

            return RedirectToPage();
        }

        private void LoadPlayers()
        {
            Players = _db.Players
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();
        }
    }
}