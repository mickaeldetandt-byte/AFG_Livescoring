using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _db;

        public LoginModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email et mot de passe obligatoires.";
                return Page();
            }

            var user = await _db.AppUsers
                .Include(u => u.Player)
                .Include(u => u.Club)
                .FirstOrDefaultAsync(u => u.Email == Email && u.IsActive);

            if (user == null)
            {
                ErrorMessage = "Utilisateur introuvable.";
                return Page();
            }

            // Version simple provisoire :
            // on compare directement le mot de passe stocké dans PasswordHash
            // On sécurisera mieux ensuite
            if (user.PasswordHash != Password)
            {
                ErrorMessage = "Mot de passe incorrect.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.PlayerId.HasValue)
                claims.Add(new Claim("PlayerId", user.PlayerId.Value.ToString()));

            if (user.ClubId.HasValue)
                claims.Add(new Claim("ClubId", user.ClubId.Value.ToString()));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            return RedirectToPage("/Competitions");
        }
    }
}