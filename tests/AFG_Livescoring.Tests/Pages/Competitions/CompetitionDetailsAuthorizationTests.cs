using System.Data.Common;
using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages.Competitions;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionDetailsAuthorizationTests
{
    [Fact]
    public async Task Admin_is_authorized_on_get()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        var result = await fixture.CreateModel(
            fixture.AdminUserId,
            fixture.CompetitionBId).OnGetAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task Owning_club_is_authorized_on_get()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        var result = await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnGetAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task Another_club_get_is_forbidden()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        var result = await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionBId).OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Missing_competition_get_returns_not_found()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        var result = await fixture.CreateModel(
            fixture.AdminUserId,
            int.MaxValue).OnGetAsync();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Forbidden_get_loads_no_detailed_business_data()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        fixture.CommandRecorder.Clear();

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionBId).OnGetAsync();

        var sql = string.Join(Environment.NewLine, fixture.CommandRecorder.Commands);
        Assert.DoesNotContain("\"Rounds\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Scores\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Squads\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Teams\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"TeamRounds\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"MatchPlayRounds\"", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valid_draft_can_start()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        var result = await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            CompetitionStatus.InProgress,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Draft_with_only_an_empty_structure_cannot_start()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        var emptyCompetitionId = await fixture.AddEmptyCompetitionAsync();

        var result = await fixture.CreateModel(
            fixture.ClubAUserId,
            emptyCompetitionId).OnPostStartAsync(emptyCompetitionId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            CompetitionStatus.Draft,
            await fixture.GetStatusAsync(emptyCompetitionId));
    }

    [Fact]
    public async Task In_progress_competition_cannot_be_started_again()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        await fixture.SetStatusAsync(
            fixture.CompetitionAId,
            CompetitionStatus.InProgress);

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.InProgress,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Finished_competition_cannot_be_started()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        await fixture.SetStatusAsync(
            fixture.CompetitionAId,
            CompetitionStatus.Finished);

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.Finished,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Draft_competition_cannot_be_finished()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostFinishAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.Draft,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task In_progress_competition_can_be_finished()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        await fixture.SetStatusAsync(
            fixture.CompetitionAId,
            CompetitionStatus.InProgress);

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostFinishAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.Finished,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Finished_competition_cannot_be_finished_again()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        await fixture.SetStatusAsync(
            fixture.CompetitionAId,
            CompetitionStatus.Finished);

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostFinishAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.Finished,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Another_club_cannot_start_a_competition()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(
            fixture.ClubBUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Another_club_cannot_finish_a_competition()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        await fixture.SetStatusAsync(
            fixture.CompetitionAId,
            CompetitionStatus.InProgress);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(
            fixture.ClubBUserId,
            fixture.CompetitionAId).OnPostFinishAsync(fixture.CompetitionAId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Starting_a_competition_does_not_modify_another_competition()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        var otherBefore = await fixture.CaptureCompetitionAsync(
            fixture.CompetitionBId);

        await fixture.CreateModel(
            fixture.AdminUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.Equal(
            otherBefore,
            await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
    }

    [Fact]
    public async Task Authorization_refusal_performs_no_write()
    {
        await using var fixture = await DetailsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        await fixture.CreateModel(
            fixture.ClubBUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Two_successive_start_requests_do_not_create_an_invalid_transition()
    {
        await using var fixture = await DetailsFixture.CreateAsync();

        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);
        await fixture.CreateModel(
            fixture.ClubAUserId,
            fixture.CompetitionAId).OnPostStartAsync(fixture.CompetitionAId);

        Assert.Equal(
            CompetitionStatus.InProgress,
            await fixture.GetStatusAsync(fixture.CompetitionAId));
    }

    private sealed class DetailsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DetailsFixture(
            SqliteConnection connection,
            AppDbContext db,
            SqlCommandRecorder commandRecorder,
            int adminUserId,
            int clubAUserId,
            int clubBUserId,
            int clubAId,
            int competitionAId,
            int competitionBId)
        {
            _connection = connection;
            Db = db;
            CommandRecorder = commandRecorder;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            ClubBUserId = clubBUserId;
            ClubAId = clubAId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
        }

        public AppDbContext Db { get; }
        public SqlCommandRecorder CommandRecorder { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int ClubBUserId { get; }
        public int ClubAId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }

        public static async Task<DetailsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var recorder = new SqlCommandRecorder();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(recorder)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var clubA = new Club { Name = "Club A" };
            var clubB = new Club { Name = "Club B" };
            db.Clubs.AddRange(clubA, clubB);
            await db.SaveChangesAsync();

            var admin = new AppUser
            {
                Email = "admin-details@example.invalid",
                Role = "Admin"
            };
            var clubAUser = new AppUser
            {
                Email = "club-a-details@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var clubBUser = new AppUser
            {
                Email = "club-b-details@example.invalid",
                Role = "Club",
                ClubId = clubB.Id
            };
            var competitionA = new Competition
            {
                Name = "Competition A",
                ClubId = clubA.Id,
                Status = CompetitionStatus.Draft,
                CompetitionType = CompetitionType.IndividualStrokePlay
            };
            var competitionB = new Competition
            {
                Name = "Competition B",
                ClubId = clubB.Id,
                Status = CompetitionStatus.Draft,
                CompetitionType = CompetitionType.IndividualStrokePlay
            };
            db.AppUsers.AddRange(admin, clubAUser, clubBUser);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            await AddValidIndividualStructureAsync(db, competitionA, "A");
            await AddValidIndividualStructureAsync(db, competitionB, "B");

            return new DetailsFixture(
                connection,
                db,
                recorder,
                admin.Id,
                clubAUser.Id,
                clubBUser.Id,
                clubA.Id,
                competitionA.Id,
                competitionB.Id);
        }

        public DetailsModel CreateModel(int userId, int competitionId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "Test"))
            };

            return new DetailsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                Id = competitionId,
                PageContext = new PageContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(
                    httpContext,
                    new TestTempDataProvider())
            };
        }

        public async Task<int> AddEmptyCompetitionAsync()
        {
            var competition = new Competition
            {
                Name = "Empty competition",
                ClubId = ClubAId,
                Status = CompetitionStatus.Draft
            };
            Db.Competitions.Add(competition);
            await Db.SaveChangesAsync();

            Db.Squads.Add(new Squad
            {
                CompetitionId = competition.Id,
                Name = "Empty squad"
            });
            await Db.SaveChangesAsync();
            return competition.Id;
        }

        public async Task SetStatusAsync(
            int competitionId,
            CompetitionStatus status)
        {
            var competition = await Db.Competitions.SingleAsync(
                item => item.Id == competitionId);
            competition.Status = status;
            await Db.SaveChangesAsync();
        }

        public Task<CompetitionStatus> GetStatusAsync(int competitionId) =>
            Db.Competitions
                .Where(item => item.Id == competitionId)
                .Select(item => item.Status)
                .SingleAsync();

        public async Task<string> CaptureAsync()
        {
            return string.Join(",", await Db.Competitions
                .OrderBy(item => item.Id)
                .Select(item => $"{item.Id}:{item.Status}:{item.ClubId}")
                .ToArrayAsync());
        }

        public async Task<string> CaptureCompetitionAsync(int competitionId)
        {
            return string.Join(
                "|",
                await Db.Competitions
                    .Where(item => item.Id == competitionId)
                    .Select(item => $"{item.Id}:{item.Status}:{item.ClubId}")
                    .SingleAsync(),
                await Db.Squads.CountAsync(item => item.CompetitionId == competitionId),
                await Db.Rounds.CountAsync(item => item.CompetitionId == competitionId));
        }

        private static async Task AddValidIndividualStructureAsync(
            AppDbContext db,
            Competition competition,
            string suffix)
        {
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = $"Squad {suffix}"
            };
            var player = new Player
            {
                FirstName = $"Player {suffix}",
                LastName = "Test"
            };
            db.Squads.Add(squad);
            db.Players.Add(player);
            await db.SaveChangesAsync();

            db.Rounds.Add(new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class SqlCommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();

        public void Clear() => Commands.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}
