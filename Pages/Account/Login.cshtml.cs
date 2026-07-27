using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages.Account
{
    public class LoginModel : PageModel
    {
        private const string GenericLoginError = "Email ou mot de passe incorrect.";

        private readonly AppDbContext _db;
        private readonly AppUserPasswordService _passwordService;

        public LoginModel(AppDbContext db, AppUserPasswordService passwordService)
        {
            _db = db;
            _passwordService = passwordService;
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
                ErrorMessage = GenericLoginError;
                return Page();
            }

            if (user.PasswordResetRequired)
            {
                ErrorMessage = GenericLoginError;
                return Page();
            }

            var verificationResult = _passwordService.VerifyPassword(user, Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ErrorMessage = GenericLoginError;
                return Page();
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordService.HashPassword(user, Password);
                user.PasswordChangedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
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
