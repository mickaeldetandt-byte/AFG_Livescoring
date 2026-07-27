using AFG_Livescoring.Models;
using AFG_Livescoring.Pages.Account;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Account;

public class LoginModelTests
{
    [Fact]
    public async Task OnPostAsync_RejectsAccountRequiringPasswordReset()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var user = new AppUser
        {
            Email = "reset-required@example.invalid",
            PasswordHash = "legacy-plain-text-value",
            PasswordResetRequired = true,
            IsActive = true
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        var model = CreateLoginModel(db);
        model.Email = user.Email;
        model.Password = "legacy-plain-text-value";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Email ou mot de passe incorrect.", model.ErrorMessage);
        Assert.Equal("legacy-plain-text-value", user.PasswordHash);
        Assert.Null(user.PasswordChangedAt);
    }

    [Fact]
    public async Task OnPostAsync_UsesSameGenericErrorForUnknownUserAndResetRequiredAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.AppUsers.Add(new AppUser
        {
            Email = "reset-required@example.invalid",
            PasswordHash = "unusable-value",
            PasswordResetRequired = true,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var resetModel = CreateLoginModel(db);
        resetModel.Email = "reset-required@example.invalid";
        resetModel.Password = "any-value";
        await resetModel.OnPostAsync();

        var unknownModel = CreateLoginModel(db);
        unknownModel.Email = "unknown@example.invalid";
        unknownModel.Password = "any-value";
        await unknownModel.OnPostAsync();

        Assert.Equal(unknownModel.ErrorMessage, resetModel.ErrorMessage);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static LoginModel CreateLoginModel(AppDbContext db)
    {
        var passwordService = new AppUserPasswordService(new PasswordHasher<AppUser>());
        return new LoginModel(db, passwordService);
    }
}
