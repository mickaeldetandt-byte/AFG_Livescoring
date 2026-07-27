using AFG_Livescoring.Models;
using Microsoft.AspNetCore.Identity;

namespace AFG_Livescoring.Services
{
    public sealed class AppUserPasswordService
    {
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public AppUserPasswordService(IPasswordHasher<AppUser> passwordHasher)
        {
            _passwordHasher = passwordHasher;
        }

        public string HashPassword(AppUser user, string password)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyPassword(AppUser user, string providedPassword)
        {
            return _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                providedPassword);
        }
    }
}
