using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages.Squads;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Squads;

public class ManageModelAuthorizationTests
{
    [Fact]
    public async Task OnPostGenerateAsync_ClubFromAnotherClub_IsForbiddenWithoutChanges()
    {
        await using var fixture = await ManageFixture.CreateAsync(roundBInSquad: true);
        var model = fixture.CreateModel(fixture.CompetitionBId);
        var squadCountBefore = await fixture.Db.Squads.CountAsync();

        var result = await model.OnPostGenerateAsync(fixture.CompetitionBId, squadSize: 4);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(squadCountBefore, await fixture.Db.Squads.CountAsync());
        Assert.Equal(
            fixture.SquadBId,
            (await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
    }

    [Fact]
    public async Task OnPostGenerateAsync_MissingCompetition_IsForbiddenWithoutChanges()
    {
        await using var fixture = await ManageFixture.CreateAsync(roundBInSquad: true);
        var model = fixture.CreateModel(int.MaxValue);
        var squadCountBefore = await fixture.Db.Squads.CountAsync();

        var result = await model.OnPostGenerateAsync(int.MaxValue, squadSize: 4);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(squadCountBefore, await fixture.Db.Squads.CountAsync());
        Assert.Equal(
            fixture.SquadBId,
            (await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
    }

    [Fact]
    public async Task OnPostGenerateAsync_TrainingWithoutParticipants_PreservesExistingSquad()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostGenerateAsync(fixture.CompetitionAId, squadSize: 3);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(await fixture.Db.Squads.AnyAsync(squad => squad.Id == fixture.SquadAId));
    }

    [Fact]
    public async Task OnPostGenerateAsync_ImpossibleDistribution_PreservesExistingStructure()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var competition = await fixture.Db.Competitions.FindAsync(fixture.CompetitionAId);
        competition!.CompetitionType = CompetitionType.MatchPlayFourball;

        var rounds = new List<Round>();
        for (var index = 1; index <= 5; index++)
        {
            var player = new Player
            {
                FirstName = $"Player {index}",
                LastName = "Impossible",
                IsActive = true
            };
            fixture.Db.Players.Add(player);
            rounds.Add(new Round
            {
                CompetitionId = fixture.CompetitionAId,
                Player = player,
                SquadId = fixture.SquadAId
            });
        }

        fixture.Db.Rounds.AddRange(rounds);
        await fixture.Db.SaveChangesAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostGenerateAsync(fixture.CompetitionAId, squadSize: 4);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(await fixture.Db.Squads.AnyAsync(squad => squad.Id == fixture.SquadAId));
        Assert.All(
            await fixture.Db.Rounds
                .Where(round => round.CompetitionId == fixture.CompetitionAId)
                .ToListAsync(),
            round => Assert.Equal(fixture.SquadAId, round.SquadId));
    }

    [Fact]
    public async Task OnPostGenerateAsync_AuthorizedTraining_GeneratesValidSquadsWithoutTouchingOtherCompetition()
    {
        await using var fixture = await ManageFixture.CreateAsync(roundBInSquad: true);

        for (var index = 1; index <= 7; index++)
        {
            var player = new Player
            {
                FirstName = $"Player {index}",
                LastName = "Valid",
                IsActive = true
            };
            fixture.Db.Players.Add(player);
            fixture.Db.Rounds.Add(new Round
            {
                CompetitionId = fixture.CompetitionAId,
                Player = player,
                SquadId = fixture.SquadAId
            });
        }

        await fixture.Db.SaveChangesAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostGenerateAsync(fixture.CompetitionAId, squadSize: 3);

        Assert.IsType<RedirectToPageResult>(result);

        var generatedSquads = await fixture.Db.Squads
            .Where(squad => squad.CompetitionId == fixture.CompetitionAId)
            .ToListAsync();
        var generatedSquadIds = generatedSquads.Select(squad => squad.Id).ToHashSet();
        var generatedRounds = await fixture.Db.Rounds
            .Where(round => round.CompetitionId == fixture.CompetitionAId)
            .ToListAsync();
        var squadSizes = generatedRounds
            .GroupBy(round => round.SquadId)
            .Select(group => group.Count())
            .ToList();

        Assert.Equal(3, generatedSquads.Count);
        Assert.Equal(7, generatedRounds.Count);
        Assert.All(generatedRounds, round => Assert.Contains(round.SquadId!.Value, generatedSquadIds));
        Assert.All(squadSizes, size => Assert.InRange(size, 1, 3));
        Assert.Equal(
            fixture.SquadBId,
            (await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
        Assert.True(await fixture.Db.Squads.AnyAsync(squad => squad.Id == fixture.SquadBId));
    }

    [Fact]
    public async Task OnGetAsync_ClubFromAnotherClub_IsForbidden()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionBId);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(model.Squads);
        Assert.Empty(model.UnassignedRounds);
    }

    [Fact]
    public async Task OnPostCreateSquadAsync_ClubFromAnotherClub_DoesNotCreateSquad()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionBId);
        var countBefore = await fixture.Db.Squads.CountAsync();

        var result = await model.OnPostCreateSquadAsync(fixture.CompetitionBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(countBefore, await fixture.Db.Squads.CountAsync());
    }

    [Fact]
    public async Task OnPostAddAndAssignAsync_ClubFromAnotherClub_DoesNotAddPlayer()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionBId);
        var countBefore = await fixture.Db.Rounds.CountAsync();

        var result = await model.OnPostAddAndAssignAsync(
            fixture.CompetitionBId,
            fixture.ActivePlayerId,
            fixture.SquadBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(countBefore, await fixture.Db.Rounds.CountAsync());
    }

    [Fact]
    public async Task OnPostAssignAsync_ClubFromAnotherClub_DoesNotAssignRound()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionBId);

        var result = await model.OnPostAssignAsync(
            fixture.CompetitionBId,
            fixture.RoundBId,
            fixture.SquadBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Null((await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
    }

    [Fact]
    public async Task OnPostRemoveAsync_ClubFromAnotherClub_DoesNotRemoveRoundFromSquad()
    {
        await using var fixture = await ManageFixture.CreateAsync(roundBInSquad: true);
        var model = fixture.CreateModel(fixture.CompetitionBId);

        var result = await model.OnPostRemoveAsync(
            fixture.CompetitionBId,
            fixture.RoundBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(
            fixture.SquadBId,
            (await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
    }

    [Fact]
    public async Task OnPostAddAndAssignAsync_SquadFromAnotherCompetition_IsRejected()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);
        var countBefore = await fixture.Db.Rounds.CountAsync();

        var result = await model.OnPostAddAndAssignAsync(
            fixture.CompetitionAId,
            fixture.ActivePlayerId,
            fixture.SquadBId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(countBefore, await fixture.Db.Rounds.CountAsync());
    }

    [Fact]
    public async Task OnPostAssignAsync_RoundFromAnotherCompetition_IsRejected()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostAssignAsync(
            fixture.CompetitionAId,
            fixture.RoundBId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null((await fixture.Db.Rounds.FindAsync(fixture.RoundBId))!.SquadId);
    }

    [Fact]
    public async Task OnPostAddAndAssignAsync_InactivePlayer_IsRejected()
    {
        await using var fixture = await ManageFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);
        var countBefore = await fixture.Db.Rounds.CountAsync();

        var result = await model.OnPostAddAndAssignAsync(
            fixture.CompetitionAId,
            fixture.InactivePlayerId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(countBefore, await fixture.Db.Rounds.CountAsync());
    }

    private sealed class ManageFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ManageFixture(
            SqliteConnection connection,
            AppDbContext db,
            int userId,
            int competitionAId,
            int competitionBId,
            int squadAId,
            int squadBId,
            int roundBId,
            int activePlayerId,
            int inactivePlayerId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
            SquadAId = squadAId;
            SquadBId = squadBId;
            RoundBId = roundBId;
            ActivePlayerId = activePlayerId;
            InactivePlayerId = inactivePlayerId;
        }

        public AppDbContext Db { get; }
        public int UserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }
        public int SquadAId { get; }
        public int SquadBId { get; }
        public int RoundBId { get; }
        public int ActivePlayerId { get; }
        public int InactivePlayerId { get; }

        public static async Task<ManageFixture> CreateAsync(bool roundBInSquad = false)
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
                Email = "club-a-squads@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competitionA = CreateTrainingCompetition("Competition A", clubA.Id);
            var competitionB = CreateTrainingCompetition("Competition B", clubB.Id);
            var activePlayer = new Player
            {
                FirstName = "Active",
                LastName = "Player",
                IsActive = true
            };
            var inactivePlayer = new Player
            {
                FirstName = "Inactive",
                LastName = "Player",
                IsActive = false
            };

            db.AppUsers.Add(user);
            db.Competitions.AddRange(competitionA, competitionB);
            db.Players.AddRange(activePlayer, inactivePlayer);
            await db.SaveChangesAsync();

            var squadA = new Squad
            {
                CompetitionId = competitionA.Id,
                Name = "Squad A",
                StartHole = 1
            };
            var squadB = new Squad
            {
                CompetitionId = competitionB.Id,
                Name = "Squad B",
                StartHole = 1
            };
            db.Squads.AddRange(squadA, squadB);
            await db.SaveChangesAsync();

            var roundB = new Round
            {
                CompetitionId = competitionB.Id,
                PlayerId = activePlayer.Id,
                SquadId = roundBInSquad ? squadB.Id : null
            };
            db.Rounds.Add(roundB);
            await db.SaveChangesAsync();

            return new ManageFixture(
                connection,
                db,
                user.Id,
                competitionA.Id,
                competitionB.Id,
                squadA.Id,
                squadB.Id,
                roundB.Id,
                activePlayer.Id,
                inactivePlayer.Id);
        }

        public ManageModel CreateModel(int competitionId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(UserId)
            };
            var authorizationService = new CompetitionAuthorizationService(Db);

            return new ManageModel(Db, authorizationService)
            {
                competitionId = competitionId,
                PageContext = new PageContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        private static Competition CreateTrainingCompetition(string name, int clubId)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                Mode = "Training",
                ScoringMode = ScoringMode.IndividualAllowed
            };
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

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
