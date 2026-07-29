using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages;

public class CompetitionParticipantsModelTests
{
    [Fact]
    public async Task OnGetAsync_ClubFromAnotherClub_IsForbidden()
    {
        await using var fixture = await ParticipantsFixture.CreateAsync();
        var model = fixture.CreateModel();

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Null(model.Competition);
        Assert.Empty(model.Players);
        Assert.Empty(model.Rounds);
    }

    [Fact]
    public async Task OnPostAddAsync_ClubFromAnotherClub_DoesNotAddParticipant()
    {
        await using var fixture = await ParticipantsFixture.CreateAsync();
        var model = fixture.CreateModel();
        model.SelectedPlayerId = fixture.PlayerId;

        var result = await model.OnPostAddAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.False(await fixture.Db.Rounds.AnyAsync());
    }

    [Fact]
    public async Task OnPostRemoveAsync_ClubFromAnotherClub_DoesNotRemoveParticipant()
    {
        await using var fixture = await ParticipantsFixture.CreateAsync(addRound: true);
        var model = fixture.CreateModel();

        var result = await model.OnPostRemoveAsync(fixture.RoundId!.Value);

        Assert.IsType<ForbidResult>(result);
        Assert.True(await fixture.Db.Rounds.AnyAsync(round => round.Id == fixture.RoundId));
    }

    private sealed class ParticipantsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ParticipantsFixture(
            SqliteConnection connection,
            AppDbContext db,
            int userId,
            int competitionId,
            int playerId,
            int? roundId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            CompetitionId = competitionId;
            PlayerId = playerId;
            RoundId = roundId;
        }

        public AppDbContext Db { get; }
        public int UserId { get; }
        public int CompetitionId { get; }
        public int PlayerId { get; }
        public int? RoundId { get; }

        public static async Task<ParticipantsFixture> CreateAsync(bool addRound = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var clubA = new Club { Name = "Club A" };
            var clubB = new Club { Name = "Club B" };
            db.Clubs.AddRange(clubA, clubB);
            await db.SaveChangesAsync();

            var user = new AppUser
            {
                Email = "club-a@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competition = new Competition
            {
                Name = "Competition du club B",
                ClubId = clubB.Id
            };
            var player = new Player
            {
                FirstName = "Joueur",
                LastName = "Test",
                IsActive = true
            };

            db.AppUsers.Add(user);
            db.Competitions.Add(competition);
            db.Players.Add(player);
            await db.SaveChangesAsync();

            Round? round = null;
            if (addRound)
            {
                round = new Round
                {
                    CompetitionId = competition.Id,
                    PlayerId = player.Id
                };
                db.Rounds.Add(round);
                await db.SaveChangesAsync();
            }

            return new ParticipantsFixture(
                connection,
                db,
                user.Id,
                competition.Id,
                player.Id,
                round?.Id);
        }

        public CompetitionParticipantsModel CreateModel()
        {
            var authorizationService = new CompetitionAuthorizationService(Db);
            var model = new CompetitionParticipantsModel(Db, authorizationService)
            {
                CompetitionId = CompetitionId,
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = CreatePrincipal(UserId)
                    }
                }
            };

            return model;
        }

        private static ClaimsPrincipal CreatePrincipal(int userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");

            return new ClaimsPrincipal(identity);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
