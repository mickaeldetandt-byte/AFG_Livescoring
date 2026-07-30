using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages.Competitions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionCreateAuthorizationTests
{
    [Fact]
    public async Task Admin_can_create_a_competition()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var competition = await fixture.Db.Competitions.SingleAsync(
            item => item.Name == CreateFixture.NewCompetitionName);
        Assert.Equal(fixture.AdminUserId, competition.CreatedByUserId);
        Assert.Null(competition.ClubId);
    }

    [Fact]
    public async Task Club_creation_automatically_uses_the_current_club()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId);

        await model.OnPostAsync();

        var competition = await fixture.GetCreatedCompetitionAsync();
        Assert.Equal(fixture.ClubAId, competition.ClubId);
        Assert.Equal(fixture.ClubAUserId, competition.CreatedByUserId);
    }

    [Fact]
    public async Task Club_without_a_club_is_forbidden()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.ClubWithoutClubUserId)
            .OnPostAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Club_without_a_club_is_also_forbidden_on_get()
    {
        await using var fixture = await CreateFixture.CreateAsync();

        var result = await fixture.CreateModel(fixture.ClubWithoutClubUserId)
            .OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Posted_club_id_is_ignored()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.ClubAUserId,
            ("Input.ClubId", fixture.ClubBId.ToString()));

        await model.OnPostAsync();

        Assert.Equal(fixture.ClubAId, (await fixture.GetCreatedCompetitionAsync()).ClubId);
    }

    [Fact]
    public async Task Posted_creator_id_is_ignored()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.ClubAUserId,
            ("Input.CreatedByUserId", fixture.AdminUserId.ToString()));

        await model.OnPostAsync();

        Assert.Equal(
            fixture.ClubAUserId,
            (await fixture.GetCreatedCompetitionAsync()).CreatedByUserId);
    }

    [Fact]
    public async Task Posted_status_is_ignored()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.AdminUserId,
            ("Input.Status", CompetitionStatus.Finished.ToString()));

        await model.OnPostAsync();

        Assert.Equal(
            CompetitionStatus.Draft,
            (await fixture.GetCreatedCompetitionAsync()).Status);
    }

    [Fact]
    public async Task Posted_is_active_is_ignored()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.AdminUserId,
            ("Input.IsActive", bool.FalseString));

        await model.OnPostAsync();

        Assert.True((await fixture.GetCreatedCompetitionAsync()).IsActive);
    }

    [Fact]
    public async Task Posted_scoring_mode_is_ignored_and_recalculated()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.AdminUserId,
            ("Input.ScoringMode", ScoringMode.IndividualAllowed.ToString()));
        model.Input.Mode = "Competition";

        await model.OnPostAsync();

        Assert.Equal(
            ScoringMode.SquadOnly,
            (await fixture.GetCreatedCompetitionAsync()).ScoringMode);
    }

    [Fact]
    public async Task Missing_course_is_rejected_without_creation()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.Input.CourseId = int.MaxValue;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Inactive_course_is_rejected_without_creation()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.Input.CourseId = fixture.InactiveCourseId;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_competition_type_is_rejected_without_creation()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.Input.CompetitionType = (CompetitionType)int.MaxValue;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_mode_is_rejected_without_creation()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.Input.Mode = "Unexpected";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_visibility_is_rejected_without_creation()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.Input.Visibility = (CompetitionVisibility)int.MaxValue;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Valid_competition_is_always_created_draft_and_active()
    {
        await using var fixture = await CreateFixture.CreateAsync();

        await fixture.CreateModel(fixture.ClubAUserId).OnPostAsync();

        var competition = await fixture.GetCreatedCompetitionAsync();
        Assert.Equal(CompetitionStatus.Draft, competition.Status);
        Assert.True(competition.IsActive);
    }

    [Fact]
    public async Task Invalid_input_never_creates_a_competition()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId);
        model.Input.Name = " ";
        model.Input.CourseId = int.MaxValue;
        model.Input.Mode = "Invalid";
        model.Input.CompetitionType = (CompetitionType)(-1);
        model.Input.Visibility = (CompetitionVisibility)(-1);

        await model.OnPostAsync();

        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Creation_never_modifies_another_clubs_data()
    {
        await using var fixture = await CreateFixture.CreateAsync();
        var otherClubBefore = await fixture.CaptureOtherClubAsync();

        await fixture.CreateModel(fixture.ClubAUserId).OnPostAsync();

        Assert.Equal(otherClubBefore, await fixture.CaptureOtherClubAsync());
    }

    [Theory]
    [InlineData(nameof(Competition.ClubId))]
    [InlineData(nameof(Competition.CreatedByUserId))]
    [InlineData(nameof(Competition.Status))]
    [InlineData(nameof(Competition.IsActive))]
    [InlineData(nameof(Competition.ScoringMode))]
    public void Input_model_excludes_every_server_owned_property(string propertyName)
    {
        Assert.Null(typeof(CreateModel.InputModel).GetProperty(propertyName));
    }

    private sealed class CreateFixture : IAsyncDisposable
    {
        public const string NewCompetitionName = "Nouvelle compétition";

        private readonly SqliteConnection _connection;

        private CreateFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int clubWithoutClubUserId,
            int clubAId,
            int clubBId,
            int activeCourseId,
            int inactiveCourseId,
            int otherCompetitionId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            ClubWithoutClubUserId = clubWithoutClubUserId;
            ClubAId = clubAId;
            ClubBId = clubBId;
            ActiveCourseId = activeCourseId;
            InactiveCourseId = inactiveCourseId;
            OtherCompetitionId = otherCompetitionId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int ClubWithoutClubUserId { get; }
        public int ClubAId { get; }
        public int ClubBId { get; }
        public int ActiveCourseId { get; }
        public int InactiveCourseId { get; }
        public int OtherCompetitionId { get; }

        public static async Task<CreateFixture> CreateAsync()
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
            var activeCourse = new Course { Name = "Actif", IsActive = true };
            var inactiveCourse = new Course { Name = "Inactif", IsActive = false };
            db.Clubs.AddRange(clubA, clubB);
            db.Courses.AddRange(activeCourse, inactiveCourse);
            await db.SaveChangesAsync();

            var admin = new AppUser
            {
                Email = "admin-create@example.invalid",
                Role = "Admin"
            };
            var clubAUser = new AppUser
            {
                Email = "club-a-create@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var clubWithoutClub = new AppUser
            {
                Email = "club-without-club@example.invalid",
                Role = "Club"
            };
            db.AppUsers.AddRange(admin, clubAUser, clubWithoutClub);
            await db.SaveChangesAsync();

            var otherCompetition = new Competition
            {
                Name = "Compétition existante du club B",
                ClubId = clubB.Id,
                CourseId = activeCourse.Id,
                CreatedByUserId = admin.Id,
                Status = CompetitionStatus.InProgress,
                IsActive = false
            };
            db.Competitions.Add(otherCompetition);
            await db.SaveChangesAsync();

            return new CreateFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                clubWithoutClub.Id,
                clubA.Id,
                clubB.Id,
                activeCourse.Id,
                inactiveCourse.Id,
                otherCompetition.Id);
        }

        public CreateModel CreateModel(
            int userId,
            params (string Key, string Value)[] postedServerFields)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "Test"))
            };

            if (postedServerFields.Length > 0)
            {
                httpContext.Request.Form = new FormCollection(
                    postedServerFields.ToDictionary(
                        field => field.Key,
                        field => new StringValues(field.Value)));
            }

            return new CreateModel(Db)
            {
                PageContext = new PageContext { HttpContext = httpContext },
                Input = new CreateModel.InputModel
                {
                    Name = NewCompetitionName,
                    Date = new DateTime(2026, 8, 15),
                    CourseId = ActiveCourseId,
                    Mode = "Competition",
                    CompetitionType = CompetitionType.IndividualStrokePlay,
                    Visibility = CompetitionVisibility.Club
                }
            };
        }

        public Task<Competition> GetCreatedCompetitionAsync() =>
            Db.Competitions.SingleAsync(item => item.Name == NewCompetitionName);

        public Task<bool> HasCreatedCompetitionAsync() =>
            Db.Competitions.AnyAsync(item => item.Name == NewCompetitionName);

        public async Task<string> CaptureAsync()
        {
            return string.Join(",", await Db.Competitions
                .OrderBy(item => item.Id)
                .Select(item =>
                    $"{item.Id}:{item.Name}:{item.ClubId}:{item.CreatedByUserId}:{item.Status}:{item.IsActive}")
                .ToArrayAsync());
        }

        public async Task<string> CaptureOtherClubAsync()
        {
            return string.Join(",", await Db.Competitions
                .Where(item => item.Id == OtherCompetitionId)
                .Select(item =>
                    $"{item.Id}:{item.Name}:{item.ClubId}:{item.CreatedByUserId}:{item.Status}:{item.IsActive}")
                .ToArrayAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
