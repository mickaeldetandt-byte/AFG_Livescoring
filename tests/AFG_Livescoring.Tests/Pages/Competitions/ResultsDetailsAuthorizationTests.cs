using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages.Competitions;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public class ResultsDetailsAuthorizationTests
{
    [Fact]
    public async Task Admin_CanViewCompetitionFromAnyClub()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId, fixture.CompetitionBId);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(fixture.CompetitionBId, model.Competition!.Id);
        Assert.Single(model.Results);
        Assert.Equal(fixture.PlayerBId, model.Results[0].PlayerId);
    }

    [Fact]
    public async Task OwnerClub_CanViewItsCompetition()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId, fixture.CompetitionAId);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(fixture.CompetitionAId, model.Competition!.Id);
    }

    [Fact]
    public async Task OtherClub_IsForbiddenOnGetWithoutWrites()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId, fixture.CompetitionBId);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Null(model.Competition);
        Assert.Empty(model.Results);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OwnerClub_CanExportItsCompetition()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId, fixture.CompetitionAId);

        var result = await model.OnGetExportExcelAsync();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task OtherClub_IsForbiddenOnExportWithoutWrites()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId, fixture.CompetitionBId);

        var result = await model.OnGetExportExcelAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Null(model.Competition);
        Assert.Empty(model.Results);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task MissingCompetition_ReturnsNotFoundWithoutWrites()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId, int.MaxValue);

        var getResult = await model.OnGetAsync();
        var exportResult = await model.OnGetExportExcelAsync();

        Assert.IsType<NotFoundResult>(getResult);
        Assert.IsType<NotFoundResult>(exportResult);
        Assert.Null(model.Competition);
        Assert.Empty(model.Results);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task ResultsContainOnlyTargetCompetitionData()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId, fixture.CompetitionAId);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var row = Assert.Single(model.Results);
        Assert.Equal(fixture.PlayerAId, row.PlayerId);
        Assert.DoesNotContain(model.Results, item => item.PlayerId == fixture.PlayerBId);
        Assert.Equal(4, row.TotalStrokes);
    }

    [Fact]
    public async Task ExportPreservesMimeTypeAndFileName()
    {
        await using var fixture = await ResultsFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId, fixture.CompetitionBId);

        var result = await model.OnGetExportExcelAsync();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.ContentType);
        Assert.StartsWith(
            $"resultats_competition_{fixture.CompetitionBId}_",
            file.FileDownloadName);
        Assert.EndsWith(".xlsx", file.FileDownloadName);
    }

    private sealed class ResultsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ResultsFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int competitionAId,
            int competitionBId,
            int playerAId,
            int playerBId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
            PlayerAId = playerAId;
            PlayerBId = playerBId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }
        public int PlayerAId { get; }
        public int PlayerBId { get; }

        public static async Task<ResultsFixture> CreateAsync()
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
            var course = new Course { Name = "Test course" };
            db.Clubs.AddRange(clubA, clubB);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            db.Holes.Add(new Hole
            {
                CourseId = course.Id,
                HoleNumber = 1,
                Par = 3
            });

            var admin = new AppUser
            {
                Email = "admin-results@example.invalid",
                Role = "Admin"
            };
            var clubAUser = new AppUser
            {
                Email = "club-a-results@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competitionA = new Competition
            {
                Name = "Competition A",
                ClubId = clubA.Id,
                CourseId = course.Id
            };
            var competitionB = new Competition
            {
                Name = "Competition B",
                ClubId = clubB.Id,
                CourseId = course.Id
            };
            db.AppUsers.AddRange(admin, clubAUser);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            var playerA = new Player { FirstName = "Alice", LastName = "A" };
            var playerB = new Player { FirstName = "Bob", LastName = "B" };
            var squadA = new Squad
            {
                CompetitionId = competitionA.Id,
                Name = "Squad A"
            };
            var squadB = new Squad
            {
                CompetitionId = competitionB.Id,
                Name = "Squad B"
            };
            db.Players.AddRange(playerA, playerB);
            db.Squads.AddRange(squadA, squadB);
            await db.SaveChangesAsync();

            var roundA = new Round
            {
                CompetitionId = competitionA.Id,
                PlayerId = playerA.Id,
                SquadId = squadA.Id
            };
            var roundB = new Round
            {
                CompetitionId = competitionB.Id,
                PlayerId = playerB.Id,
                SquadId = squadB.Id
            };
            db.Rounds.AddRange(roundA, roundB);
            await db.SaveChangesAsync();

            db.Scores.AddRange(
                new Score
                {
                    RoundId = roundA.Id,
                    HoleNumber = 1,
                    Strokes = 4
                },
                new Score
                {
                    RoundId = roundB.Id,
                    HoleNumber = 1,
                    Strokes = 7
                });
            await db.SaveChangesAsync();

            return new ResultsFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                competitionA.Id,
                competitionB.Id,
                playerA.Id,
                playerB.Id);
        }

        public ResultsDetailsModel CreateModel(int userId, int competitionId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "Test"))
            };

            return new ResultsDetailsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                CompetitionId = competitionId,
                PageContext = new PageContext { HttpContext = httpContext }
            };
        }

        public async Task<DatabaseSnapshot> CaptureAsync()
        {
            return new DatabaseSnapshot(
                string.Join(",", await Db.Competitions
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.ClubId}:{item.Name}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Rounds
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.PlayerId}:{item.SquadId}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Scores
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.RoundId}:{item.HoleNumber}:{item.Strokes}")
                    .ToArrayAsync()));
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
