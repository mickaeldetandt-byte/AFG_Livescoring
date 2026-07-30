using System.Data.Common;
using System.Security.Claims;
using AFG_Livescoring.Models;
using AFG_Livescoring.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AFG_Livescoring.Tests.Pages;

public sealed class LeaderboardPublicAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_in_progress_competition_is_accessible(bool display)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (model, result) = fixture.Invoke(
            display,
            fixture.PublicInProgressId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CompetitionStatus.InProgress, model.Competition!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_finished_competition_is_accessible(bool display)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (model, result) = fixture.Invoke(display, fixture.PublicFinishedId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CompetitionStatus.Finished, model.Competition!.Status);
    }

    [Theory]
    [InlineData(false, PublicAccessCase.Draft)]
    [InlineData(true, PublicAccessCase.Draft)]
    [InlineData(false, PublicAccessCase.Inactive)]
    [InlineData(true, PublicAccessCase.Inactive)]
    [InlineData(false, PublicAccessCase.Private)]
    [InlineData(true, PublicAccessCase.Private)]
    [InlineData(false, PublicAccessCase.Club)]
    [InlineData(true, PublicAccessCase.Club)]
    [InlineData(false, PublicAccessCase.Missing)]
    [InlineData(true, PublicAccessCase.Missing)]
    public async Task Non_public_competitions_are_indistinguishable_not_found(
        bool display,
        PublicAccessCase accessCase)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();
        var competitionId = fixture.GetCompetitionId(accessCase);
        var before = await fixture.CaptureDataAsync();
        fixture.CommandInterceptor.Clear();

        var (model, result) = fixture.Invoke(display, competitionId);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.Competition);
        Assert.Empty(model.Rows);
        Assert.Empty(model.MatchPlayRows);
        Assert.DoesNotContain(
            fixture.CommandInterceptor.Commands,
            IsDetailedSportsQuery);
        Assert.Equal(before, await fixture.CaptureDataAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Individual_leaderboard_still_loads_scores(bool display)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (model, result) = fixture.Invoke(
            display,
            fixture.PublicInProgressId);

        Assert.IsType<PageResult>(result);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Alice Individual", row.PlayerName);
        Assert.Equal(1, row.HolesPlayed);
        Assert.Equal(4, row.TotalStrokes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Doubles_leaderboard_still_loads_team_scores(bool display)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (model, result) = fixture.Invoke(display, fixture.PublicDoublesId);

        Assert.IsType<PageResult>(result);
        var row = Assert.Single(model.Rows);
        Assert.Contains("Bob Doubles", row.PlayerName);
        Assert.Equal(1, row.HolesPlayed);
        Assert.Equal(3, row.TotalStrokes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Match_play_leaderboard_still_loads_matches(bool display)
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (model, result) = fixture.Invoke(display, fixture.PublicMatchPlayId);

        Assert.IsType<PageResult>(result);
        var match = Assert.Single(model.MatchPlayRows);
        Assert.Equal("Team A", match.TeamAName);
        Assert.Equal("Team B", match.TeamBName);
    }

    [Fact]
    public async Task Leaderboard_and_display_are_read_only()
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();
        var before = await fixture.CaptureDataAsync();

        fixture.Invoke(false, fixture.PublicInProgressId);
        fixture.Invoke(true, fixture.PublicDoublesId);
        fixture.Invoke(false, fixture.PublicMatchPlayId);

        Assert.Equal(before, await fixture.CaptureDataAsync());
    }

    [Fact]
    public async Task Admin_authenticated_access_is_unchanged()
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (_, result) = fixture.InvokeAuthenticated(
            display: false,
            fixture.PublicInactiveId,
            role: "Admin",
            email: "admin@example.invalid");

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task Club_authenticated_access_to_its_internal_competition_is_unchanged()
    {
        await using var fixture = await PublicAccessFixture.CreateAsync();

        var (_, result) = fixture.InvokeAuthenticated(
            display: true,
            fixture.ClubOnlyId,
            role: "Club",
            email: fixture.ClubUserEmail);

        Assert.IsType<PageResult>(result);
    }

    private static bool IsDetailedSportsQuery(string command)
    {
        var detailedTables = new[]
        {
            "\"Rounds\"",
            "\"Scores\"",
            "\"Players\"",
            "\"Squads\"",
            "\"Teams\"",
            "\"TeamPlayers\"",
            "\"TeamRounds\"",
            "\"TeamScores\"",
            "\"MatchPlayRounds\"",
            "\"MatchPlayHoleResults\""
        };

        return detailedTables.Any(command.Contains);
    }

    public enum PublicAccessCase
    {
        Draft,
        Inactive,
        Private,
        Club,
        Missing
    }

    private sealed class PublicAccessFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PublicAccessFixture(
            SqliteConnection connection,
            AppDbContext db,
            RecordingCommandInterceptor commandInterceptor,
            int publicInProgressId,
            int publicFinishedId,
            int publicDoublesId,
            int publicMatchPlayId,
            int publicDraftId,
            int publicInactiveId,
            int privateId,
            int clubId,
            string clubUserEmail)
        {
            _connection = connection;
            Db = db;
            CommandInterceptor = commandInterceptor;
            PublicInProgressId = publicInProgressId;
            PublicFinishedId = publicFinishedId;
            PublicDoublesId = publicDoublesId;
            PublicMatchPlayId = publicMatchPlayId;
            PublicDraftId = publicDraftId;
            PublicInactiveId = publicInactiveId;
            PrivateId = privateId;
            ClubOnlyId = clubId;
            ClubUserEmail = clubUserEmail;
        }

        public AppDbContext Db { get; }
        public RecordingCommandInterceptor CommandInterceptor { get; }
        public int PublicInProgressId { get; }
        public int PublicFinishedId { get; }
        public int PublicDoublesId { get; }
        public int PublicMatchPlayId { get; }
        public int PublicDraftId { get; }
        public int PublicInactiveId { get; }
        public int PrivateId { get; }
        public int ClubOnlyId { get; }
        public string ClubUserEmail { get; }

        public static async Task<PublicAccessFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var interceptor = new RecordingCommandInterceptor();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var course = new Course { Name = "Parcours public", IsActive = true };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            const string clubUserEmail = "club-public-access@example.invalid";
            var clubUser = new AppUser
            {
                Email = clubUserEmail,
                Role = "Club"
            };
            db.AppUsers.Add(clubUser);
            await db.SaveChangesAsync();
            db.Holes.Add(new Hole
            {
                CourseId = course.Id,
                HoleNumber = 1,
                Par = 4
            });

            Competition Competition(
                string name,
                CompetitionStatus status,
                CompetitionVisibility visibility = CompetitionVisibility.Public,
                bool isActive = true,
                CompetitionType type = CompetitionType.IndividualStrokePlay) =>
                new()
                {
                    Name = name,
                    CourseId = course.Id,
                    Status = status,
                    Visibility = visibility,
                    IsActive = isActive,
                    CompetitionType = type
                };

            var publicInProgress = Competition(
                "Publique en cours",
                CompetitionStatus.InProgress);
            var publicFinished = Competition(
                "Publique terminée",
                CompetitionStatus.Finished);
            var publicDoubles = Competition(
                "Doubles public",
                CompetitionStatus.InProgress,
                type: CompetitionType.DoublesFourball);
            var publicMatchPlay = Competition(
                "Match Play public",
                CompetitionStatus.InProgress,
                type: CompetitionType.MatchPlayIndividual);
            var publicDraft = Competition(
                "Brouillon public",
                CompetitionStatus.Draft);
            var publicInactive = Competition(
                "Publique inactive",
                CompetitionStatus.InProgress,
                isActive: false);
            var privateCompetition = Competition(
                "Privée",
                CompetitionStatus.InProgress,
                CompetitionVisibility.Private);
            var clubCompetition = Competition(
                "Interne",
                CompetitionStatus.InProgress,
                CompetitionVisibility.Club);
            clubCompetition.CreatedByUserId = clubUser.Id;
            db.Competitions.AddRange(
                publicInProgress,
                publicFinished,
                publicDoubles,
                publicMatchPlay,
                publicDraft,
                publicInactive,
                privateCompetition,
                clubCompetition);
            await db.SaveChangesAsync();

            var individualPlayer = new Player
            {
                FirstName = "Alice",
                LastName = "Individual"
            };
            var doublesPlayer = new Player
            {
                FirstName = "Bob",
                LastName = "Doubles"
            };
            var matchPlayerA = new Player
            {
                FirstName = "Team",
                LastName = "A"
            };
            var matchPlayerB = new Player
            {
                FirstName = "Team",
                LastName = "B"
            };
            db.Players.AddRange(
                individualPlayer,
                doublesPlayer,
                matchPlayerA,
                matchPlayerB);
            await db.SaveChangesAsync();

            var individualRound = new Round
            {
                CompetitionId = publicInProgress.Id,
                PlayerId = individualPlayer.Id
            };
            var doublesSquad = new Squad
            {
                CompetitionId = publicDoubles.Id,
                Name = "Doubles",
                StartHole = 1
            };
            var matchSquad = new Squad
            {
                CompetitionId = publicMatchPlay.Id,
                Name = "Match",
                StartHole = 1
            };
            db.AddRange(individualRound, doublesSquad, matchSquad);
            await db.SaveChangesAsync();
            db.Scores.Add(new Score
            {
                RoundId = individualRound.Id,
                HoleNumber = 1,
                Strokes = 4
            });

            var doublesTeam = new Team
            {
                CompetitionId = publicDoubles.Id,
                SquadId = doublesSquad.Id,
                Name = "Doubles team"
            };
            var matchTeamA = new Team
            {
                CompetitionId = publicMatchPlay.Id,
                SquadId = matchSquad.Id,
                Name = "Team A"
            };
            var matchTeamB = new Team
            {
                CompetitionId = publicMatchPlay.Id,
                SquadId = matchSquad.Id,
                Name = "Team B"
            };
            db.Teams.AddRange(doublesTeam, matchTeamA, matchTeamB);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer
                {
                    TeamId = doublesTeam.Id,
                    PlayerId = doublesPlayer.Id,
                    Order = 1
                },
                new TeamPlayer
                {
                    TeamId = matchTeamA.Id,
                    PlayerId = matchPlayerA.Id,
                    Order = 1
                },
                new TeamPlayer
                {
                    TeamId = matchTeamB.Id,
                    PlayerId = matchPlayerB.Id,
                    Order = 1
                });
            var teamRound = new TeamRound
            {
                CompetitionId = publicDoubles.Id,
                TeamId = doublesTeam.Id,
                SquadId = doublesSquad.Id
            };
            db.TeamRounds.Add(teamRound);
            await db.SaveChangesAsync();
            db.TeamScores.Add(new TeamScore
            {
                TeamRoundId = teamRound.Id,
                HoleNumber = 1,
                Strokes = 3
            });
            db.MatchPlayRounds.Add(new MatchPlayRound
            {
                CompetitionId = publicMatchPlay.Id,
                SquadId = matchSquad.Id,
                TeamAId = matchTeamA.Id,
                TeamBId = matchTeamB.Id,
                CurrentHole = 1,
                StatusText = "AS"
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            interceptor.Clear();

            return new PublicAccessFixture(
                connection,
                db,
                interceptor,
                publicInProgress.Id,
                publicFinished.Id,
                publicDoubles.Id,
                publicMatchPlay.Id,
                publicDraft.Id,
                publicInactive.Id,
                privateCompetition.Id,
                clubCompetition.Id,
                clubUserEmail);
        }

        public (LeaderboardModel Model, IActionResult Result) Invoke(
            bool display,
            int competitionId)
        {
            var httpContext = new DefaultHttpContext();
            LeaderboardModel model = display
                ? new DisplayModel(Db)
                : new LeaderboardModel(Db);
            model.PageContext = new PageContext { HttpContext = httpContext };
            model.TempData = new TempDataDictionary(
                httpContext,
                new NullTempDataProvider());
            model.CompetitionId = competitionId;
            return (model, model.OnGet());
        }

        public (LeaderboardModel Model, IActionResult Result) InvokeAuthenticated(
            bool display,
            int competitionId,
            string role,
            string email)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Role, role),
                        new Claim(ClaimTypes.Name, email)
                    },
                    authenticationType: "Test"))
            };
            LeaderboardModel model = display
                ? new DisplayModel(Db)
                : new LeaderboardModel(Db);
            model.PageContext = new PageContext { HttpContext = httpContext };
            model.TempData = new TempDataDictionary(
                httpContext,
                new NullTempDataProvider());
            model.CompetitionId = competitionId;
            return (model, model.OnGet());
        }

        public int GetCompetitionId(PublicAccessCase accessCase) =>
            accessCase switch
            {
                PublicAccessCase.Draft => PublicDraftId,
                PublicAccessCase.Inactive => PublicInactiveId,
                PublicAccessCase.Private => PrivateId,
                PublicAccessCase.Club => ClubOnlyId,
                PublicAccessCase.Missing => int.MaxValue,
                _ => throw new ArgumentOutOfRangeException(nameof(accessCase))
            };

        public async Task<string> CaptureDataAsync()
        {
            var counts = new[]
            {
                await Db.Competitions.CountAsync(),
                await Db.Rounds.CountAsync(),
                await Db.Scores.CountAsync(),
                await Db.Teams.CountAsync(),
                await Db.TeamPlayers.CountAsync(),
                await Db.TeamRounds.CountAsync(),
                await Db.TeamScores.CountAsync(),
                await Db.MatchPlayRounds.CountAsync()
            };
            return string.Join("|", counts);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor
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

        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
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
