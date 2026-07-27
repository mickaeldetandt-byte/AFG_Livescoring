using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFG_Livescoring.Tests.Services;

public class AppUserPasswordServiceTests
{
    [Fact]
    public void HashPassword_DoesNotStorePlainText_AndCanBeVerified()
    {
        var user = new AppUser { Email = "player@example.invalid" };
        var service = CreateService();

        user.PasswordHash = service.HashPassword(user, "A-strong-test-password!");

        Assert.NotEqual("A-strong-test-password!", user.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            service.VerifyPassword(user, "A-strong-test-password!"));
    }

    [Fact]
    public void HashPassword_UsesDifferentSaltForSamePassword()
    {
        var firstUser = new AppUser { Email = "first@example.invalid" };
        var secondUser = new AppUser { Email = "second@example.invalid" };
        var service = CreateService();

        var firstHash = service.HashPassword(firstUser, "Same-test-password!");
        var secondHash = service.HashPassword(secondUser, "Same-test-password!");

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void VerifyPassword_RejectsIncorrectPassword()
    {
        var user = new AppUser { Email = "player@example.invalid" };
        var service = CreateService();
        user.PasswordHash = service.HashPassword(user, "Correct-test-password!");

        var result = service.VerifyPassword(user, "Incorrect-test-password!");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyPassword_RequestsRehashForIdentityV2Hash()
    {
        var user = new AppUser { Email = "legacy@example.invalid" };
        var legacyHasher = new PasswordHasher<AppUser>(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
            }));
        user.PasswordHash = legacyHasher.HashPassword(user, "Legacy-test-password!");
        var currentService = CreateService();

        var result = currentService.VerifyPassword(user, "Legacy-test-password!");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    private static AppUserPasswordService CreateService()
    {
        return new AppUserPasswordService(new PasswordHasher<AppUser>());
    }
}
