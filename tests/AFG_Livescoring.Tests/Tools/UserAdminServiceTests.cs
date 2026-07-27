using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using AFG.UserAdmin;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Tools;

public class UserAdminServiceTests
{
    [Fact]
    public async Task AuditAsync_ReturnsAllowedFieldsWithoutChangingData()
    {
        await using var fixture = await UserAdminFixture.CreateAsync();
        var club = new Club { Name = "Test Club" };
        var player = new Player { FirstName = "Alice", LastName = "Example" };
        fixture.Db.AddRange(club, player);
        await fixture.Db.SaveChangesAsync();

        var user = new AppUser
        {
            Email = "audit@example.invalid",
            PasswordHash = "sensitive-hash-value",
            Role = "Club",
            IsActive = true,
            PasswordResetRequired = true,
            ClubId = club.Id,
            PlayerId = player.Id
        };
        fixture.Db.AppUsers.Add(user);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var rows = await fixture.Service.AuditAsync();
        using var writer = new StringWriter();
        UserAuditWriter.Write(writer, rows);
        var output = writer.ToString();

        var row = Assert.Single(rows);
        Assert.Equal(user.Id, row.Id);
        Assert.Equal(club.Id, row.ClubId);
        Assert.Equal(player.Id, row.PlayerId);
        Assert.DoesNotContain("sensitive-hash-value", output);
        Assert.DoesNotContain("PasswordHash", output);

        var unchanged = await fixture.Db.AppUsers.AsNoTracking().SingleAsync();
        Assert.Equal("sensitive-hash-value", unchanged.PasswordHash);
        Assert.True(unchanged.PasswordResetRequired);
    }

    [Fact]
    public async Task EnsureAdminAsync_CreatesSecureAdministratorOnEmptyDatabase()
    {
        await using var fixture = await UserAdminFixture.CreateAsync();

        var result = await fixture.Service.EnsureAdminAsync(
            "admin@example.invalid",
            "A-secure-password-123!");

        var user = await fixture.Db.AppUsers.SingleAsync();
        Assert.True(result.Created);
        Assert.Equal("Admin", user.Role);
        Assert.True(user.IsActive);
        Assert.False(user.PasswordResetRequired);
        Assert.NotNull(user.PasswordChangedAt);
        Assert.Null(user.ClubId);
        Assert.Null(user.PlayerId);
        Assert.NotEqual("A-secure-password-123!", user.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            fixture.PasswordService.VerifyPassword(
                user,
                "A-secure-password-123!"));
    }

    [Fact]
    public async Task EnsureAdminAsync_RestoresExistingAccountAndPreservesLinks()
    {
        await using var fixture = await UserAdminFixture.CreateAsync();
        var club = new Club { Name = "Existing Club" };
        var player = new Player { FirstName = "Bob", LastName = "Existing" };
        fixture.Db.AddRange(club, player);
        await fixture.Db.SaveChangesAsync();

        var user = new AppUser
        {
            Email = "existing@example.invalid",
            PasswordHash = "old-unusable-value",
            Role = "Player",
            IsActive = false,
            PasswordResetRequired = true,
            ClubId = club.Id,
            PlayerId = player.Id
        };
        fixture.Db.AppUsers.Add(user);
        await fixture.Db.SaveChangesAsync();
        var originalId = user.Id;

        var result = await fixture.Service.EnsureAdminAsync(
            "EXISTING@example.invalid",
            "A-new-secure-password!");

        fixture.Db.ChangeTracker.Clear();
        var restored = await fixture.Db.AppUsers.SingleAsync();
        Assert.False(result.Created);
        Assert.Equal(originalId, restored.Id);
        Assert.Equal(club.Id, restored.ClubId);
        Assert.Equal(player.Id, restored.PlayerId);
        Assert.Equal("Admin", restored.Role);
        Assert.True(restored.IsActive);
        Assert.False(restored.PasswordResetRequired);
        Assert.NotNull(restored.PasswordChangedAt);
    }

    [Fact]
    public async Task EnsureAdminAsync_RejectsShortPasswordWithoutWriting()
    {
        await using var fixture = await UserAdminFixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<UserAdminException>(
            () => fixture.Service.EnsureAdminAsync(
                "admin@example.invalid",
                "too-short"));

        Assert.Contains("12", exception.Message);
        Assert.Empty(await fixture.Db.AppUsers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task EnsureAdminAsync_RejectsAmbiguousDuplicateEmail()
    {
        await using var fixture = await UserAdminFixture.CreateAsync();
        fixture.Db.AppUsers.AddRange(
            new AppUser
            {
                Email = "duplicate@example.invalid",
                PasswordHash = "first",
                PasswordResetRequired = true
            },
            new AppUser
            {
                Email = "DUPLICATE@example.invalid",
                PasswordHash = "second",
                PasswordResetRequired = true
            });
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UserAdminException>(
            () => fixture.Service.EnsureAdminAsync(
                "duplicate@example.invalid",
                "A-secure-password-123!"));

        var users = await fixture.Db.AppUsers.AsNoTracking().ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, user => Assert.True(user.PasswordResetRequired));
    }

    private sealed class UserAdminFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private UserAdminFixture(
            SqliteConnection connection,
            AppDbContext db,
            AppUserPasswordService passwordService)
        {
            _connection = connection;
            Db = db;
            PasswordService = passwordService;
            Service = new UserAdminService(db, passwordService);
        }

        public AppDbContext Db { get; }
        public AppUserPasswordService PasswordService { get; }
        public UserAdminService Service { get; }

        public static async Task<UserAdminFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var passwordService = new AppUserPasswordService(
                new PasswordHasher<AppUser>());

            return new UserAdminFixture(connection, db, passwordService);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
