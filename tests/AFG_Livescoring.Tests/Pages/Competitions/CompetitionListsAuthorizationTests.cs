using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using AFG_Livescoring.Services;
using AFG_Livescoring.Pages.Competitions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public class CompetitionListsAuthorizationTests
{
    [Fact]
    public async Task Admin_SeesCompetitionsFromAllClubs()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateCompetitionsModel(fixture.AdminUserId);

        var result = model.OnGet();

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            new[]
            {
                fixture.CompetitionAId,
                fixture.CompetitionBId,
                fixture.PrivateCompetitionBId
            }.OrderBy(id => id),
            model.Competitions.Select(item => item.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ClubA_SeesOnlyItsCompetitions()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateCompetitionsModel(fixture.ClubAUserId);

        var result = model.OnGet();

        Assert.IsType<PageResult>(result);
        var competition = Assert.Single(model.Competitions);
        Assert.Equal(fixture.CompetitionAId, competition.Id);
        Assert.DoesNotContain(model.Competitions, item => item.Id == fixture.CompetitionBId);
        Assert.DoesNotContain(model.Competitions, item => item.Id == fixture.PrivateCompetitionBId);
    }

    [Fact]
    public async Task PrivateCompetitionFromAnotherClub_IsNeverLoaded()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateCompetitionsModel(fixture.ClubAUserId);

        var result = model.OnGet();

        Assert.IsType<PageResult>(result);
        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.PrivateCompetitionBId);
        Assert.DoesNotContain(
            fixture.CompetitionBId,
            model.CompetitionStates.Keys);
        Assert.DoesNotContain(
            fixture.PrivateCompetitionBId,
            model.CompetitionStates.Keys);
    }

    [Fact]
    public async Task Player_IsForbiddenFromAdministrationPage()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateCompetitionsModel(fixture.PlayerUserId);

        var result = model.OnGet();

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(model.Competitions);
        Assert.Empty(model.CompetitionStates);
    }

    [Fact]
    public async Task Results_AreLimitedToCurrentClub()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateResultsModel(fixture.ClubAUserId);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var competition = Assert.Single(model.Competitions);
        Assert.Equal(fixture.CompetitionAId, competition.Id);
        Assert.DoesNotContain(model.Competitions, item => item.Id == fixture.CompetitionBId);
        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.PrivateCompetitionBId);
    }

    [Fact]
    public async Task Admin_SeesEverythingInResults()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var model = fixture.CreateResultsModel(fixture.AdminUserId);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(3, model.Competitions.Count);
        Assert.Contains(model.Competitions, item => item.Id == fixture.CompetitionAId);
        Assert.Contains(model.Competitions, item => item.Id == fixture.CompetitionBId);
        Assert.Contains(
            model.Competitions,
            item => item.Id == fixture.PrivateCompetitionBId);
    }

    [Fact]
    public async Task ClubWithoutCompetitions_GetsEmptyLists()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var administrationModel = fixture.CreateCompetitionsModel(fixture.EmptyClubUserId);
        var resultsModel = fixture.CreateResultsModel(fixture.EmptyClubUserId);

        var administrationResult = administrationModel.OnGet();
        var resultsResult = await resultsModel.OnGetAsync();

        Assert.IsType<PageResult>(administrationResult);
        Assert.IsType<PageResult>(resultsResult);
        Assert.Empty(administrationModel.Competitions);
        Assert.Empty(administrationModel.CompetitionStates);
        Assert.Empty(resultsModel.Competitions);
    }

    [Fact]
    public async Task AuthorizedCompetitionMetricsRemainCorrect()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var administrationModel = fixture.CreateCompetitionsModel(fixture.ClubAUserId);
        var resultsModel = fixture.CreateResultsModel(fixture.ClubAUserId);

        administrationModel.OnGet();
        await resultsModel.OnGetAsync();

        var state = administrationModel.CompetitionStates[fixture.CompetitionAId];
        Assert.Equal(1, state.PlayerCount);
        Assert.True(state.HasStarted);
        Assert.True(state.IsFinished);
        Assert.Equal(1, state.CompletedRounds);

        var resultRow = Assert.Single(resultsModel.Competitions);
        Assert.Equal(1, resultRow.PlayerCount);
        Assert.True(resultRow.HasStarted);
        Assert.True(resultRow.IsFinished);
    }

    [Fact]
    public async Task ReadingBothLists_DoesNotWriteToDatabase()
    {
        await using var fixture = await ListsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        fixture.CreateCompetitionsModel(fixture.AdminUserId).OnGet();
        await fixture.CreateResultsModel(fixture.AdminUserId).OnGetAsync();

        Assert.Equal(before, await fixture.CaptureAsync());
    }

    private sealed class ListsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ListsFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int playerUserId,
            int emptyClubUserId,
            int competitionAId,
            int competitionBId,
            int privateCompetitionBId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            PlayerUserId = playerUserId;
            EmptyClubUserId = emptyClubUserId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
            PrivateCompetitionBId = privateCompetitionBId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int PlayerUserId { get; }
        public int EmptyClubUserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }
        public int PrivateCompetitionBId { get; }

        public static async Task<ListsFixture> CreateAsync()
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
            var emptyClub = new Club { Name = "Club without competitions" };
            var course = new Course { Name = "Test course", IsActive = true };
            db.Clubs.AddRange(clubA, clubB, emptyClub);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var admin = CreateUser("admin-lists@example.invalid", "Admin", null);
            var clubAUser = CreateUser("club-a-lists@example.invalid", "Club", clubA.Id);
            var playerUser = CreateUser("player-lists@example.invalid", "Player", null);
            var emptyClubUser = CreateUser(
                "empty-club-lists@example.invalid",
                "Club",
                emptyClub.Id);
            db.AppUsers.AddRange(admin, clubAUser, playerUser, emptyClubUser);

            var competitionA = CreateCompetition(
                "Competition A",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Private);
            competitionA.Status = CompetitionStatus.Finished;
            var competitionB = CreateCompetition(
                "Competition B",
                clubB.Id,
                course.Id,
                CompetitionVisibility.Public);
            competitionB.Status = CompetitionStatus.InProgress;
            var privateCompetitionB = CreateCompetition(
                "Private competition B",
                clubB.Id,
                course.Id,
                CompetitionVisibility.Private);
            db.Competitions.AddRange(
                competitionA,
                competitionB,
                privateCompetitionB);
            await db.SaveChangesAsync();

            var playerA = new Player { FirstName = "Alice", LastName = "A" };
            var playerB = new Player { FirstName = "Bob", LastName = "B" };
            db.Players.AddRange(playerA, playerB);
            await db.SaveChangesAsync();

            var roundA = new Round
            {
                CompetitionId = competitionA.Id,
                PlayerId = playerA.Id
            };
            var roundB = new Round
            {
                CompetitionId = competitionB.Id,
                PlayerId = playerB.Id
            };
            db.Rounds.AddRange(roundA, roundB);
            await db.SaveChangesAsync();

            db.Scores.AddRange(Enumerable.Range(1, 18).Select(hole => new Score
            {
                RoundId = roundA.Id,
                HoleNumber = hole,
                Strokes = 4
            }));
            db.Scores.Add(new Score
            {
                RoundId = roundB.Id,
                HoleNumber = 1,
                Strokes = 5
            });
            await db.SaveChangesAsync();

            return new ListsFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                playerUser.Id,
                emptyClubUser.Id,
                competitionA.Id,
                competitionB.Id,
                privateCompetitionB.Id);
        }

        public CompetitionsModel CreateCompetitionsModel(int userId)
        {
            return new CompetitionsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                PageContext = CreatePageContext(userId)
            };
        }

        public ResultsModel CreateResultsModel(int userId)
        {
            return new ResultsModel(Db)
            {
                PageContext = CreatePageContext(userId)
            };
        }

        public async Task<DatabaseSnapshot> CaptureAsync()
        {
            return new DatabaseSnapshot(
                string.Join(",", await Db.Competitions
                    .OrderBy(item => item.Id)
                    .Select(item =>
                        $"{item.Id}:{item.ClubId}:{item.Name}:{item.Visibility}:{item.Status}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Rounds
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.PlayerId}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Scores
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.RoundId}:{item.HoleNumber}:{item.Strokes}")
                    .ToArrayAsync()));
        }

        private PageContext CreatePageContext(int userId)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test"));
            return new PageContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static AppUser CreateUser(
            string email,
            string role,
            int? clubId)
        {
            return new AppUser
            {
                Email = email,
                Role = role,
                ClubId = clubId
            };
        }

        private static Competition CreateCompetition(
            string name,
            int clubId,
            int courseId,
            CompetitionVisibility visibility)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CourseId = courseId,
                Visibility = visibility,
                IsActive = true
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record DatabaseSnapshot(
        string Competitions,
        string Rounds,
        string Scores);
}
