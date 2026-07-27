using System.Net.Mail;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using Microsoft.EntityFrameworkCore;

namespace AFG.UserAdmin;

public sealed record UserAuditRow(
    int Id,
    string Email,
    string Role,
    bool IsActive,
    bool PasswordResetRequired,
    int? ClubId,
    string? ClubName,
    int? PlayerId,
    string? PlayerName);

public sealed record EnsureAdminResult(
    int UserId,
    string Email,
    bool Created);

public sealed class UserAdminException : Exception
{
    public UserAdminException(string message) : base(message)
    {
    }
}

public sealed class UserAdminService
{
    private const int MinimumPasswordLength = 12;

    private readonly AppDbContext _db;
    private readonly AppUserPasswordService _passwordService;

    public UserAdminService(
        AppDbContext db,
        AppUserPasswordService passwordService)
    {
        _db = db;
        _passwordService = passwordService;
    }

    public Task<List<UserAuditRow>> AuditAsync()
    {
        return _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserAuditRow(
                user.Id,
                user.Email,
                user.Role,
                user.IsActive,
                user.PasswordResetRequired,
                user.ClubId,
                user.Club == null ? null : user.Club.Name,
                user.PlayerId,
                user.Player == null
                    ? null
                    : user.Player.FirstName + " " + user.Player.LastName))
            .ToListAsync();
    }

    public async Task<EnsureAdminResult> EnsureAdminAsync(
        string email,
        string password)
    {
        var normalizedEmail = ValidateAndNormalizeEmail(email);
        ValidatePassword(password);

        var matchingUsers = await _db.AppUsers
            .Where(user => user.Email.ToLower() == normalizedEmail)
            .ToListAsync();

        if (matchingUsers.Count > 1)
        {
            throw new UserAdminException(
                "Plusieurs comptes utilisent cet email. Aucune modification effectuée.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var user = matchingUsers.SingleOrDefault();
        var created = user == null;

        if (created)
        {
            user = new AppUser
            {
                Email = email.Trim(),
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.AppUsers.Add(user);
        }
        else
        {
            user!.Role = "Admin";
            user.IsActive = true;
        }

        user.PasswordHash = _passwordService.HashPassword(user, password);
        user.PasswordResetRequired = false;
        user.PasswordChangedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new EnsureAdminResult(user.Id, user.Email, created);
    }

    private static string ValidateAndNormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserAdminException("L'email est obligatoire.");
        }

        try
        {
            var parsed = new MailAddress(email.Trim());
            if (!string.Equals(
                    parsed.Address,
                    email.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new UserAdminException("L'email est invalide.");
            }
        }
        catch (FormatException)
        {
            throw new UserAdminException("L'email est invalide.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < MinimumPasswordLength)
        {
            throw new UserAdminException(
                $"Le mot de passe doit contenir au moins {MinimumPasswordLength} caractères.");
        }
    }
}
