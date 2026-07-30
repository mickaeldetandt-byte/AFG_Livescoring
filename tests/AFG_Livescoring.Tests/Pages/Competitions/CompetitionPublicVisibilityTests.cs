using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using AFG_Livescoring.Pages.Competitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionPublicVisibilityTests
{
    [Fact]
    public async Task Page_is_explicitly_accessible_anonymously()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.NotEmpty(model.Competitions);
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(PublicModel),
            typeof(AllowAnonymousAttribute)));
    }

    [Fact]
    public async Task Private_competition_is_absent()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.PrivateCompetitionId);
    }

    [Fact]
    public async Task Internal_club_competition_is_absent()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.InternalCompetitionId);
    }

    [Fact]
    public async Task Inactive_competition_is_absent()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.InactiveCompetitionId);
    }

    [Fact]
    public async Task Public_draft_is_absent()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.DraftCompetitionId);
    }

    [Fact]
    public async Task Public_in_progress_competition_is_present()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.Contains(
            model.Competitions,
            item => item.Id == fixture.InProgressCompetitionId);
    }

    [Fact]
    public async Task Public_finished_competition_is_present()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.Contains(
            model.Competitions,
            item => item.Id == fixture.FinishedCompetitionId);
    }

    [Fact]
    public async Task Public_metrics_still_use_format_aware_calculation()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        var competition = model.Competitions.Single(
            item => item.Id == fixture.InProgressCompetitionId);
        Assert.Equal(1, competition.PlayerCount);
        Assert.Equal(1, competition.TotalRounds);
        Assert.Equal(1, competition.CompletedRounds);
        Assert.True(competition.HasStarted);
    }

    [Fact]
    public void View_contains_only_anonymous_competition_links()
    {
        var markup = File.ReadAllText(FindPublicViewPath());

        Assert.Contains("asp-page=\"/Leaderboard\"", markup);
        Assert.Contains("asp-page=\"/Display\"", markup);
        Assert.DoesNotContain("asp-page=\"/Competitions/ResultsDetails\"", markup);
        Assert.DoesNotContain("asp-page=\"/Competitions/Results\"", markup);
        Assert.DoesNotContain("asp-page=\"/Competitions/Details\"", markup);
        Assert.Empty(typeof(LeaderboardModel).GetCustomAttributes(
            typeof(AuthorizeAttribute),
            inherit: true));
        Assert.Empty(typeof(DisplayModel).GetCustomAttributes(
            typeof(AuthorizeAttribute),
            inherit: true));
    }

    [Fact]
    public async Task Empty_public_list_is_handled()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var visibleCompetitions = await fixture.Db.Competitions
            .Where(item => item.Status != CompetitionStatus.Draft)
            .ToListAsync();
        foreach (var competition in visibleCompetitions)
            competition.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var model = fixture.CreateModel();
        await model.OnGetAsync();

        Assert.Empty(model.Competitions);
    }

    [Fact]
    public async Task Public_page_performs_no_write()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        await fixture.CreateModel().OnGetAsync();

        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Public_rows_expose_no_club_or_creator_identifier()
    {
        await using var fixture = await PublicFixture.CreateAsync();
        var model = fixture.CreateModel();

        await model.OnGetAsync();

        Assert.NotEmpty(model.Competitions);
        Assert.Null(typeof(PublicModel.PublicCompetitionRow).GetProperty("ClubId"));
        Assert.Null(typeof(PublicModel.PublicCompetitionRow).GetProperty("CreatedByUserId"));
        Assert.DoesNotContain(
            model.Competitions,
            item => item.Id == fixture.OtherClubPrivateCompetitionId);
    }

    private static string FindPublicViewPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "Pages",
                "Competitions",
                "Public.cshtml");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Pages/Competitions/Public.cshtml introuvable.");
    }

    private sealed class PublicFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PublicFixture(
            SqliteConnection connection,
            AppDbContext db,
            int privateCompetitionId,
            int internalCompetitionId,
            int inactiveCompetitionId,
            int draftCompetitionId,
            int inProgressCompetitionId,
            int finishedCompetitionId,
            int otherClubPrivateCompetitionId)
        {
            _connection = connection;
            Db = db;
            PrivateCompetitionId = privateCompetitionId;
            InternalCompetitionId = internalCompetitionId;
            InactiveCompetitionId = inactiveCompetitionId;
            DraftCompetitionId = draftCompetitionId;
            InProgressCompetitionId = inProgressCompetitionId;
            FinishedCompetitionId = finishedCompetitionId;
            OtherClubPrivateCompetitionId = otherClubPrivateCompetitionId;
        }

        public AppDbContext Db { get; }
        public int PrivateCompetitionId { get; }
        public int InternalCompetitionId { get; }
        public int InactiveCompetitionId { get; }
        public int DraftCompetitionId { get; }
        public int InProgressCompetitionId { get; }
        public int FinishedCompetitionId { get; }
        public int OtherClubPrivateCompetitionId { get; }

        public static async Task<PublicFixture> CreateAsync()
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
            var course = new Course { Name = "Public course", IsActive = true };
            db.Clubs.AddRange(clubA, clubB);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var privateCompetition = CreateCompetition(
                "Private",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Private,
                CompetitionStatus.InProgress);
            var internalCompetition = CreateCompetition(
                "Internal",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Club,
                CompetitionStatus.InProgress);
            var inactiveCompetition = CreateCompetition(
                "Inactive",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Public,
                CompetitionStatus.InProgress,
                isActive: false);
            var draftCompetition = CreateCompetition(
                "Draft",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Public,
                CompetitionStatus.Draft);
            var inProgressCompetition = CreateCompetition(
                "In progress",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Public,
                CompetitionStatus.InProgress);
            var finishedCompetition = CreateCompetition(
                "Finished",
                clubA.Id,
                course.Id,
                CompetitionVisibility.Public,
                CompetitionStatus.Finished);
            var otherClubPrivateCompetition = CreateCompetition(
                "Other club secret",
                clubB.Id,
                course.Id,
                CompetitionVisibility.Private,
                CompetitionStatus.Finished);
            db.Competitions.AddRange(
                privateCompetition,
                internalCompetition,
                inactiveCompetition,
                draftCompetition,
                inProgressCompetition,
                finishedCompetition,
                otherClubPrivateCompetition);
            await db.SaveChangesAsync();

            await AddCompletedIndividualRoundAsync(db, inProgressCompetition);

            return new PublicFixture(
                connection,
                db,
                privateCompetition.Id,
                internalCompetition.Id,
                inactiveCompetition.Id,
                draftCompetition.Id,
                inProgressCompetition.Id,
                finishedCompetition.Id,
                otherClubPrivateCompetition.Id);
        }

        public PublicModel CreateModel()
        {
            return new PublicModel(Db)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        public async Task<string> CaptureAsync()
        {
            return string.Join(
                "|",
                string.Join(",", await Db.Competitions
                    .OrderBy(item => item.Id)
                    .Select(item =>
                        $"{item.Id}:{item.Visibility}:{item.IsActive}:{item.Status}")
                    .ToArrayAsync()),
                await Db.Rounds.CountAsync(),
                await Db.Scores.CountAsync());
        }

        private static Competition CreateCompetition(
            string name,
            int clubId,
            int courseId,
            CompetitionVisibility visibility,
            CompetitionStatus status,
            bool isActive = true)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CourseId = courseId,
                Visibility = visibility,
                Status = status,
                IsActive = isActive,
                CompetitionType = CompetitionType.IndividualStrokePlay
            };
        }

        private static async Task AddCompletedIndividualRoundAsync(
            AppDbContext db,
            Competition competition)
        {
            var player = new Player { FirstName = "Public", LastName = "Player" };
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = "Public squad"
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

            db.Scores.AddRange(Enumerable.Range(1, 18).Select(hole => new Score
            {
                RoundId = round.Id,
                HoleNumber = hole,
                Strokes = 4
            }));
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
