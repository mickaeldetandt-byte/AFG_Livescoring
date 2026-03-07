using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AFG_Livescoring.Models; // adapte selon ton namespace

public class PlayersModel : PageModel
{
    private readonly AppDbContext _db;

    public PlayersModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Player> Players { get; set; } = new();

    [BindProperty]
    public Player NewPlayer { get; set; } = new();

    public void OnGet()
    {
        Players = _db.Players
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToList();
    }

    public IActionResult OnPostAdd()
    {
        if (string.IsNullOrWhiteSpace(NewPlayer.FirstName) || string.IsNullOrWhiteSpace(NewPlayer.LastName))
        {
            // recharge la liste pour ré-afficher la page proprement
            Players = _db.Players.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
            ModelState.AddModelError(string.Empty, "Prénom et nom obligatoires.");
            return Page();
        }

        _db.Players.Add(NewPlayer);
        _db.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        var player = _db.Players.Find(id);
        if (player != null)
        {
            _db.Players.Remove(player);
            _db.SaveChanges();
        }

        return RedirectToPage();
    }
}