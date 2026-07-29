using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Services;

public class CompetitionAuthorizationServiceTests
{
    [Fact]
    public async Task Admin_OnCompetitionFromAnotherClub_IsAllowed()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(
            userRole: "Admin",
            userClubId: 1,
            competitionClubId: 2);

        var result = await fixture.Service.CanManageCompetitionAsync(
            CreatePrincipal(fixture.UserId),
            fixture.CompetitionId);

        Assert.True(result);
    }

    [Fact]
    public async Task Club_OnOwnCompetition_IsAllowed()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(
            userRole: "Club",
            userClubId: 1,
            competitionClubId: 1);

        var result = await fixture.Service.CanManageCompetitionAsync(
            CreatePrincipal(fixture.UserId),
            fixture.CompetitionId);

        Assert.True(result);
    }

    [Fact]
    public async Task Club_OnCompetitionFromAnotherClub_IsDenied()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(
            userRole: "Club",
            userClubId: 1,
            competitionClubId: 2);

        var result = await fixture.Service.CanManageCompetitionAsync(
            CreatePrincipal(fixture.UserId),
            fixture.CompetitionId);

        Assert.False(result);
    }

    [Fact]
    public async Task UserWithoutClub_IsDenied()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(
            userRole: "Club",
            userClubId: null,
            competitionClubId: 1);

        var result = await fixture.Service.CanManageCompetitionAsync(
            CreatePrincipal(fixture.UserId),
            fixture.CompetitionId);

        Assert.False(result);
    }

    [Fact]
    public async Task Player_IsDenied()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(
            userRole: "Player",
            userClubId: 1,
            competitionClubId: 1);

        var result = await fixture.Service.CanManageCompetitionAsync(
            CreatePrincipal(fixture.UserId),
            fixture.CompetitionId);

        Assert.False(result);
    }

    private static ClaimsPrincipal CreatePrincipal(int userId)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }

    private sealed class AuthorizationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AppDbContext _db;

        private AuthorizationFixture(
            SqliteConnection connection,
            AppDbContext db,
            int userId,
            int competitionId)
        {
            _connection = connection;
            _db = db;
            UserId = userId;
            CompetitionId = competitionId;
            Service = new CompetitionAuthorizationService(db);
        }

        public int UserId { get; }
        public int CompetitionId { get; }
        public CompetitionAuthorizationService Service { get; }

        public static async Task<AuthorizationFixture> CreateAsync(
            string userRole,
            int? userClubId,
            int? competitionClubId)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.Clubs.AddRange(
                new Club { Id = 1, Name = "Club A" },
                new Club { Id = 2, Name = "Club B" });

            var user = new AppUser
            {
                Email = "authorization-test@example.invalid",
                Role = userRole,
                ClubId = userClubId
            };

            var competition = new Competition
            {
                Name = "Competition test",
                ClubId = competitionClubId
            };

            db.AppUsers.Add(user);
            db.Competitions.Add(competition);
            await db.SaveChangesAsync();

            return new AuthorizationFixture(connection, db, user.Id, competition.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
