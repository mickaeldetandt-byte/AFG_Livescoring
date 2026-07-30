using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using AFG_Livescoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionMainCreateAuthorizationTests
{
    [Fact]
    public async Task Admin_can_create_with_server_owned_values()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);

        var result = model.OnPostAdd();

        Assert.IsType<RedirectToPageResult>(result);
        var competition = await fixture.GetCreatedCompetitionAsync();
        Assert.Equal(fixture.AdminUserId, competition.CreatedByUserId);
        Assert.Null(competition.ClubId);
        Assert.Equal(CompetitionStatus.Draft, competition.Status);
        Assert.True(competition.IsActive);
        Assert.Equal(ScoringMode.SquadOnly, competition.ScoringMode);
    }

    [Fact]
    public async Task Club_creation_uses_the_current_users_club()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();

        fixture.CreateModel(fixture.ClubAUserId).OnPostAdd();

        var competition = await fixture.GetCreatedCompetitionAsync();
        Assert.Equal(fixture.ClubAId, competition.ClubId);
        Assert.Equal(fixture.ClubAUserId, competition.CreatedByUserId);
    }

    [Fact]
    public async Task Club_without_a_club_is_forbidden_without_writing()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = fixture.CreateModel(fixture.ClubWithoutClubUserId)
            .OnPostAdd();

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Non_admin_or_club_database_role_is_forbidden()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();

        var result = fixture.CreateModel(fixture.PlayerUserId).OnPostAdd();

        Assert.IsType<ForbidResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Missing_course_is_rejected_without_creation()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.NewCompetition.CourseId = int.MaxValue;

        var result = model.OnPostAdd();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Inactive_course_is_rejected_without_creation()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.NewCompetition.CourseId = fixture.InactiveCourseId;

        var result = model.OnPostAdd();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_mode_is_rejected_without_creation()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.NewCompetition.Mode = "Unexpected";

        var result = model.OnPostAdd();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_type_is_rejected_without_creation()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.NewCompetition.CompetitionType = (CompetitionType)int.MaxValue;

        var result = model.OnPostAdd();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Invalid_visibility_is_rejected_without_creation()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.AdminUserId);
        model.NewCompetition.Visibility = (CompetitionVisibility)int.MaxValue;

        var result = model.OnPostAdd();

        Assert.IsType<PageResult>(result);
        Assert.False(await fixture.HasCreatedCompetitionAsync());
    }

    [Fact]
    public async Task Training_mode_calculates_individual_scoring()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(fixture.ClubAUserId);
        model.NewCompetition.Mode = "Training";

        model.OnPostAdd();

        Assert.Equal(
            ScoringMode.IndividualAllowed,
            (await fixture.GetCreatedCompetitionAsync()).ScoringMode);
    }

    [Fact]
    public async Task Posted_server_owned_values_are_ignored()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var model = fixture.CreateModel(
            fixture.ClubAUserId,
            ("NewCompetition.ClubId", fixture.ClubBId.ToString()),
            ("NewCompetition.CreatedByUserId", fixture.AdminUserId.ToString()),
            ("NewCompetition.Status", CompetitionStatus.Finished.ToString()),
            ("NewCompetition.IsActive", bool.FalseString),
            ("NewCompetition.ScoringMode", ScoringMode.IndividualAllowed.ToString()));

        model.OnPostAdd();

        var competition = await fixture.GetCreatedCompetitionAsync();
        Assert.Equal(fixture.ClubAId, competition.ClubId);
        Assert.Equal(fixture.ClubAUserId, competition.CreatedByUserId);
        Assert.Equal(CompetitionStatus.Draft, competition.Status);
        Assert.True(competition.IsActive);
        Assert.Equal(ScoringMode.SquadOnly, competition.ScoringMode);
    }

    [Theory]
    [InlineData(nameof(Competition.ClubId))]
    [InlineData(nameof(Competition.CreatedByUserId))]
    [InlineData(nameof(Competition.Status))]
    [InlineData(nameof(Competition.IsActive))]
    [InlineData(nameof(Competition.ScoringMode))]
    public void Input_model_excludes_server_owned_properties(string propertyName)
    {
        Assert.Null(
            typeof(CompetitionsModel.CompetitionInputModel)
                .GetProperty(propertyName));
    }

    [Fact]
    public async Task Creation_does_not_modify_another_clubs_data()
    {
        await using var fixture = await MainCreateFixture.CreateAsync();
        var otherCompetitionBefore = await fixture.GetOtherCompetitionAsync();

        fixture.CreateModel(fixture.ClubAUserId).OnPostAdd();

        Assert.Equal(
            otherCompetitionBefore,
            await fixture.GetOtherCompetitionAsync());
    }

    private sealed class MainCreateFixture : IAsyncDisposable
    {
        private const string NewCompetitionName = "Création principale sécurisée";
        private readonly SqliteConnection _connection;

        private MainCreateFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int clubWithoutClubUserId,
            int playerUserId,
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
            PlayerUserId = playerUserId;
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
        public int PlayerUserId { get; }
        public int ClubAId { get; }
        public int ClubBId { get; }
        public int ActiveCourseId { get; }
        public int InactiveCourseId { get; }
        public int OtherCompetitionId { get; }

        public static async Task<MainCreateFixture> CreateAsync()
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
            db.AddRange(clubA, clubB, activeCourse, inactiveCourse);
            await db.SaveChangesAsync();

            var admin = new AppUser
            {
                Email = "admin-main-create@example.invalid",
                Role = "Admin"
            };
            var clubAUser = new AppUser
            {
                Email = "club-a-main-create@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var clubWithoutClub = new AppUser
            {
                Email = "club-without-main-create@example.invalid",
                Role = "Club"
            };
            var player = new AppUser
            {
                Email = "player-main-create@example.invalid",
                Role = "Player"
            };
            db.AddRange(admin, clubAUser, clubWithoutClub, player);
            await db.SaveChangesAsync();

            var otherCompetition = new Competition
            {
                Name = "Compétition du club B",
                ClubId = clubB.Id,
                CourseId = activeCourse.Id,
                CreatedByUserId = admin.Id,
                Status = CompetitionStatus.InProgress,
                IsActive = false
            };
            db.Competitions.Add(otherCompetition);
            await db.SaveChangesAsync();

            return new MainCreateFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                clubWithoutClub.Id,
                player.Id,
                clubA.Id,
                clubB.Id,
                activeCourse.Id,
                inactiveCourse.Id,
                otherCompetition.Id);
        }

        public CompetitionsModel CreateModel(
            int userId,
            params (string Key, string Value)[] postedServerFields)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Club")
                    },
                    authenticationType: "Test"))
            };

            if (postedServerFields.Length > 0)
            {
                httpContext.Request.Form = new FormCollection(
                    postedServerFields.ToDictionary(
                        field => field.Key,
                        field => new StringValues(field.Value)));
            }

            var model = new CompetitionsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                PageContext = new PageContext { HttpContext = httpContext },
                NewCompetition = new CompetitionsModel.CompetitionInputModel
                {
                    Name = NewCompetitionName,
                    Date = new DateTime(2026, 8, 1),
                    CourseId = ActiveCourseId,
                    Mode = "Competition",
                    CompetitionType = CompetitionType.IndividualStrokePlay,
                    Visibility = CompetitionVisibility.Public
                }
            };
            model.TempData = new TempDataDictionary(
                httpContext,
                new NullTempDataProvider());
            return model;
        }

        public Task<Competition> GetCreatedCompetitionAsync() =>
            Db.Competitions
                .AsNoTracking()
                .SingleAsync(item => item.Name == NewCompetitionName);

        public Task<bool> HasCreatedCompetitionAsync() =>
            Db.Competitions.AnyAsync(item => item.Name == NewCompetitionName);

        public async Task<string> GetOtherCompetitionAsync()
        {
            var item = await Db.Competitions
                .AsNoTracking()
                .SingleAsync(competition => competition.Id == OtherCompetitionId);
            return $"{item.Id}|{item.Name}|{item.ClubId}|{item.Status}|{item.IsActive}";
        }

        public async Task<string> CaptureAsync()
        {
            var competitions = await Db.Competitions
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => $"{item.Id}|{item.Name}|{item.ClubId}")
                .ToListAsync();
            return string.Join(Environment.NewLine, competitions);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
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
