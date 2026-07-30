using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using AFG_Livescoring.Pages.Competitions;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionStructuredLoggingTests
{
    [Fact]
    public async Task Successful_creation_writes_a_structured_entry()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<CreateModel>();
        var model = fixture.CreateDedicatedCreationModel(
            fixture.AdminUserId,
            logger);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var created = await fixture.Db.Competitions.SingleAsync(
            item => item.Name == "Logged creation");
        var entry = Assert.Single(
            logger.Entries,
            item => item.PropertyEquals("Operation", "CreateDedicated")
                    && item.PropertyEquals("CompetitionId", created.Id));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.True(entry.PropertyEquals("UserId", fixture.AdminUserId));
    }

    [Fact]
    public async Task Cross_club_refusal_writes_a_warning()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<DetailsModel>();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateDetailsModel(
                fixture.ClubBUserId,
                fixture.CompetitionAId,
                logger)
            .OnPostStartAsync(fixture.CompetitionAId);

        Assert.IsType<ForbidResult>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.PropertyEquals("Operation", "Start")
            && entry.PropertyEquals("Reason", "CrossClubAccess"));
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Deletion_blocked_by_results_is_logged_without_a_write()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<CompetitionsModel>();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateCompetitionsModel(
                fixture.AdminUserId,
                logger)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.PropertyEquals("Operation", "Delete")
            && entry.PropertyEquals("Reason", "SportsResultsExist"));
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Status_transition_logs_old_and_new_status()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<DetailsModel>();

        await fixture.CreateDetailsModel(
                fixture.ClubBUserId,
                fixture.CompetitionBId,
                logger)
            .OnPostStartAsync(fixture.CompetitionBId);

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information
            && entry.PropertyEquals("Operation", "Start")
            && entry.PropertyEquals("OldStatus", CompetitionStatus.Draft)
            && entry.PropertyEquals("NewStatus", CompetitionStatus.InProgress));
        Assert.Equal(
            CompetitionStatus.InProgress,
            await fixture.Db.Competitions
                .Where(item => item.Id == fixture.CompetitionBId)
                .Select(item => item.Status)
                .SingleAsync());
    }

    [Fact]
    public async Task Authorized_export_writes_a_success_entry()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<ResultsDetailsModel>();

        var result = await fixture.CreateResultsDetailsModel(
                fixture.ClubAUserId,
                fixture.CompetitionAId,
                logger)
            .OnGetExportExcelAsync();

        Assert.IsType<FileContentResult>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information
            && entry.PropertyEquals("Operation", "ExportExcel")
            && entry.PropertyEquals("CompetitionId", fixture.CompetitionAId));
    }

    [Fact]
    public async Task Refused_export_writes_a_cross_club_warning()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<ResultsDetailsModel>();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateResultsDetailsModel(
                fixture.ClubBUserId,
                fixture.CompetitionAId,
                logger)
            .OnGetExportExcelAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.PropertyEquals("Operation", "ExportExcel")
            && entry.PropertyEquals("Reason", "CrossClubAccess"));
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Log_entries_contain_no_secret_or_score_detail()
    {
        await using var fixture = await LoggingFixture.CreateAsync();
        var logger = new TestLogger<ResultsDetailsModel>();

        await fixture.CreateResultsDetailsModel(
                fixture.ClubAUserId,
                fixture.CompetitionAId,
                logger)
            .OnGetExportExcelAsync();

        var serializedLogs = string.Join(
            Environment.NewLine,
            logger.Entries.Select(entry =>
                entry.Message + " " + string.Join(",", entry.Properties)));
        Assert.DoesNotContain("Password", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HoleNumber", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Strokes", serializedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlayerName", serializedLogs, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LoggingFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private LoggingFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int clubBUserId,
            int activeCourseId,
            int competitionAId,
            int competitionBId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            ClubBUserId = clubBUserId;
            ActiveCourseId = activeCourseId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int ClubBUserId { get; }
        public int ActiveCourseId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }

        public static async Task<LoggingFixture> CreateAsync()
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
            var course = new Course { Name = "Logging course", IsActive = true };
            db.Clubs.AddRange(clubA, clubB);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            db.Holes.AddRange(Enumerable.Range(1, 18).Select(hole => new Hole
            {
                CourseId = course.Id,
                HoleNumber = hole,
                Par = 3
            }));

            var admin = CreateUser("admin-log@example.invalid", "Admin", null);
            var clubAUser = CreateUser("club-a-log@example.invalid", "Club", clubA.Id);
            var clubBUser = CreateUser("club-b-log@example.invalid", "Club", clubB.Id);
            var competitionA = CreateCompetition("Competition A", clubA.Id, course.Id);
            var competitionB = CreateCompetition("Competition B", clubB.Id, course.Id);
            db.AppUsers.AddRange(admin, clubAUser, clubBUser);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            await AddStructureAsync(db, competitionA, "A", addScore: true);
            await AddStructureAsync(db, competitionB, "B", addScore: false);

            return new LoggingFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                clubBUser.Id,
                course.Id,
                competitionA.Id,
                competitionB.Id);
        }

        public CreateModel CreateDedicatedCreationModel(
            int userId,
            ILogger<CreateModel> logger)
        {
            var context = CreateHttpContext(userId);
            return new CreateModel(Db, logger)
            {
                PageContext = new PageContext { HttpContext = context },
                Input = new CreateModel.InputModel
                {
                    Name = "Logged creation",
                    Date = DateTime.Today,
                    CourseId = ActiveCourseId,
                    Mode = "Competition",
                    CompetitionType = CompetitionType.IndividualStrokePlay,
                    Visibility = CompetitionVisibility.Private
                }
            };
        }

        public CompetitionsModel CreateCompetitionsModel(
            int userId,
            ILogger<CompetitionsModel> logger)
        {
            var context = CreateHttpContext(userId);
            return new CompetitionsModel(
                Db,
                new CompetitionAuthorizationService(Db),
                logger)
            {
                PageContext = new PageContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new TestTempDataProvider())
            };
        }

        public DetailsModel CreateDetailsModel(
            int userId,
            int competitionId,
            ILogger<DetailsModel> logger)
        {
            var context = CreateHttpContext(userId);
            return new DetailsModel(
                Db,
                new CompetitionAuthorizationService(Db),
                logger)
            {
                Id = competitionId,
                PageContext = new PageContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new TestTempDataProvider())
            };
        }

        public ResultsDetailsModel CreateResultsDetailsModel(
            int userId,
            int competitionId,
            ILogger<ResultsDetailsModel> logger)
        {
            var context = CreateHttpContext(userId);
            return new ResultsDetailsModel(
                Db,
                new CompetitionAuthorizationService(Db),
                logger)
            {
                CompetitionId = competitionId,
                PageContext = new PageContext { HttpContext = context }
            };
        }

        public async Task<string> CaptureAsync()
        {
            return string.Join(
                "|",
                string.Join(",", await Db.Competitions
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.Status}").ToArrayAsync()),
                await Db.Rounds.CountAsync(),
                await Db.Scores.CountAsync());
        }

        private DefaultHttpContext CreateHttpContext(int userId)
        {
            var user = Db.AppUsers.AsNoTracking().Single(item => item.Id == userId);
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Email),
                        new Claim(ClaimTypes.Role, user.Role)
                    },
                    authenticationType: "Test"))
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
            int courseId)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CourseId = courseId,
                Status = CompetitionStatus.Draft
            };
        }

        private static async Task AddStructureAsync(
            AppDbContext db,
            Competition competition,
            string suffix,
            bool addScore)
        {
            var player = new Player
            {
                FirstName = $"Player {suffix}",
                LastName = "Test"
            };
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = $"Squad {suffix}"
            };
            db.Players.Add(player);
            db.Squads.Add(squad);
            await db.SaveChangesAsync();

            var round = new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id
            };
            db.Rounds.Add(round);
            await db.SaveChangesAsync();

            if (addScore)
            {
                db.Scores.Add(new Score
                {
                    RoundId = round.Id,
                    HoleNumber = 1,
                    Strokes = 4
                });
                await db.SaveChangesAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values
                    .Where(item => item.Key != "{OriginalFormat}")
                    .ToDictionary(item => item.Key, item => item.Value)
                : new Dictionary<string, object?>();

            Entries.Add(new LogEntry(
                logLevel,
                formatter(state, exception),
                properties,
                exception));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception)
    {
        public bool PropertyEquals(string name, object expected)
        {
            return Properties.TryGetValue(name, out var actual)
                   && string.Equals(
                       actual?.ToString(),
                       expected.ToString(),
                       StringComparison.Ordinal);
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
