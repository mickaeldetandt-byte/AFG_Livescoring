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
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionMetricsConsistencyTests
{
    [Theory]
    [InlineData(CompetitionStatus.Draft, false, false)]
    [InlineData(CompetitionStatus.InProgress, true, false)]
    [InlineData(CompetitionStatus.Finished, true, true)]
    public async Task Official_state_always_comes_from_competition_status(
        CompetitionStatus status,
        bool expectedStarted,
        bool expectedFinished)
    {
        await using var fixture = await MetricsFixture.CreateAsync();
        var competition = await fixture.Db.Competitions.SingleAsync(
            item => item.Id == fixture.IndividualCompetitionId);
        competition.Status = status;
        await fixture.Db.SaveChangesAsync();

        var metrics = (await CompetitionMetricsCalculator.CalculateAsync(
            fixture.Db,
            new[] { competition }))[competition.Id];

        Assert.Equal(expectedStarted, metrics.HasStarted);
        Assert.Equal(expectedFinished, metrics.IsFinished);
    }

    [Fact]
    public async Task Individual_metrics_use_rounds_and_individual_scores()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var metrics = await fixture.GetMetricsAsync(
            fixture.IndividualCompetitionId);

        Assert.Equal(2, metrics.ParticipantsCount);
        Assert.Equal(2, metrics.TotalRounds);
        Assert.Equal(1, metrics.StartedRounds);
        Assert.Equal(1, metrics.CompletedRounds);
        Assert.False(metrics.HasStarted);
    }

    [Fact]
    public async Task Doubles_metrics_use_team_rounds_and_team_scores()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var metrics = await fixture.GetMetricsAsync(
            fixture.DoublesCompetitionId);

        Assert.Equal(4, metrics.ParticipantsCount);
        Assert.Equal(2, metrics.TotalRounds);
        Assert.Equal(2, metrics.StartedRounds);
        Assert.Equal(1, metrics.CompletedRounds);
        Assert.True(metrics.HasStarted);
        Assert.False(metrics.IsFinished);
    }

    [Fact]
    public async Task Match_play_metrics_use_matches_results_and_match_status()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var metrics = await fixture.GetMetricsAsync(
            fixture.MatchPlayCompetitionId);

        Assert.Equal(2, metrics.ParticipantsCount);
        Assert.Equal(2, metrics.TotalRounds);
        Assert.Equal(2, metrics.StartedRounds);
        Assert.Equal(1, metrics.CompletedRounds);
        Assert.True(metrics.HasStarted);
        Assert.True(metrics.IsFinished);
    }

    [Fact]
    public async Task Metrics_never_mix_another_competitions_data()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var metrics = await fixture.GetMetricsAsync(
            fixture.IndividualCompetitionId);

        Assert.Equal(2, metrics.TotalRounds);
        Assert.Equal(1, metrics.CompletedRounds);
        Assert.Equal(2, metrics.ParticipantsCount);
    }

    [Fact]
    public async Task All_four_pages_return_the_same_metrics()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var administration = fixture.CreateAdministrationModel(
            fixture.AdminUserId);
        Assert.IsType<PageResult>(administration.OnGet());

        var publicPage = fixture.CreatePublicModel();
        await publicPage.OnGetAsync();

        var results = fixture.CreateResultsModel(fixture.AdminUserId);
        Assert.IsType<PageResult>(await results.OnGetAsync());

        var details = fixture.CreateDetailsModel(
            fixture.AdminUserId,
            fixture.DoublesCompetitionId);
        Assert.IsType<PageResult>(await details.OnGetAsync());

        var administrationMetrics =
            administration.CompetitionStates[fixture.DoublesCompetitionId];
        var publicMetrics = publicPage.Competitions.Single(
            item => item.Id == fixture.DoublesCompetitionId);
        var resultsMetrics = results.Competitions.Single(
            item => item.Id == fixture.DoublesCompetitionId);

        Assert.Equal(administrationMetrics.PlayerCount, publicMetrics.PlayerCount);
        Assert.Equal(administrationMetrics.PlayerCount, resultsMetrics.PlayerCount);
        Assert.Equal(administrationMetrics.PlayerCount, details.PlayerCount);
        Assert.Equal(administrationMetrics.HasStarted, publicMetrics.HasStarted);
        Assert.Equal(administrationMetrics.HasStarted, resultsMetrics.HasStarted);
        Assert.Equal(administrationMetrics.HasStarted, details.HasStarted);
        Assert.Equal(
            administrationMetrics.CompletedRounds,
            publicMetrics.CompletedRounds);
        Assert.Equal(
            administrationMetrics.CompletedRounds,
            resultsMetrics.CompletedRounds);
        Assert.Equal(administrationMetrics.CompletedRounds, details.CompletedRounds);
        Assert.Equal(administrationMetrics.TotalRounds, publicMetrics.TotalRounds);
        Assert.Equal(administrationMetrics.TotalRounds, resultsMetrics.TotalRounds);
        Assert.Equal(administrationMetrics.TotalRounds, details.TotalRounds);
    }

    [Fact]
    public async Task Empty_competition_list_is_handled()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var metrics = await CompetitionMetricsCalculator.CalculateAsync(
            fixture.Db,
            Array.Empty<Competition>());

        Assert.Empty(metrics);
    }

    [Fact]
    public async Task Metrics_are_read_only()
    {
        await using var fixture = await MetricsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        await fixture.CreatePublicModel().OnGetAsync();
        await fixture.CreateResultsModel(fixture.AdminUserId).OnGetAsync();
        await fixture.CreateDetailsModel(
            fixture.AdminUserId,
            fixture.MatchPlayCompetitionId).OnGetAsync();

        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Existing_club_authorization_remains_unchanged()
    {
        await using var fixture = await MetricsFixture.CreateAsync();

        var administration = fixture.CreateAdministrationModel(
            fixture.ClubAUserId);
        Assert.IsType<PageResult>(administration.OnGet());
        Assert.DoesNotContain(
            fixture.OtherClubCompetitionId,
            administration.CompetitionStates.Keys);

        var results = fixture.CreateResultsModel(fixture.ClubAUserId);
        Assert.IsType<PageResult>(await results.OnGetAsync());
        Assert.DoesNotContain(
            results.Competitions,
            item => item.Id == fixture.OtherClubCompetitionId);

        var forbiddenDetails = fixture.CreateDetailsModel(
            fixture.ClubAUserId,
            fixture.OtherClubCompetitionId);
        Assert.IsType<ForbidResult>(await forbiddenDetails.OnGetAsync());
    }

    private sealed class MetricsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private MetricsFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int individualCompetitionId,
            int doublesCompetitionId,
            int matchPlayCompetitionId,
            int otherClubCompetitionId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            IndividualCompetitionId = individualCompetitionId;
            DoublesCompetitionId = doublesCompetitionId;
            MatchPlayCompetitionId = matchPlayCompetitionId;
            OtherClubCompetitionId = otherClubCompetitionId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int IndividualCompetitionId { get; }
        public int DoublesCompetitionId { get; }
        public int MatchPlayCompetitionId { get; }
        public int OtherClubCompetitionId { get; }

        public static async Task<MetricsFixture> CreateAsync()
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
            var course = new Course { Name = "Course", IsActive = true };
            db.Clubs.AddRange(clubA, clubB);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var admin = new AppUser
            {
                Email = "admin-metrics@example.invalid",
                Role = "Admin"
            };
            var clubAUser = new AppUser
            {
                Email = "club-a-metrics@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            db.AppUsers.AddRange(admin, clubAUser);

            var individual = CreateCompetition(
                "Individual",
                clubA.Id,
                course.Id,
                CompetitionType.IndividualStrokePlay,
                CompetitionStatus.Draft);
            var doubles = CreateCompetition(
                "Doubles",
                clubA.Id,
                course.Id,
                CompetitionType.DoublesFourball,
                CompetitionStatus.InProgress);
            var matchPlay = CreateCompetition(
                "Match Play",
                clubA.Id,
                course.Id,
                CompetitionType.MatchPlayIndividual,
                CompetitionStatus.Finished);
            var otherClub = CreateCompetition(
                "Other club",
                clubB.Id,
                course.Id,
                CompetitionType.IndividualStrokePlay,
                CompetitionStatus.Finished);
            db.Competitions.AddRange(individual, doubles, matchPlay, otherClub);
            await db.SaveChangesAsync();

            await AddIndividualDataAsync(db, individual, "I");
            await AddDoublesDataAsync(db, doubles);
            await AddMatchPlayDataAsync(db, matchPlay);
            await AddIndividualDataAsync(db, otherClub, "O", completeBoth: true);

            return new MetricsFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                individual.Id,
                doubles.Id,
                matchPlay.Id,
                otherClub.Id);
        }

        public async Task<CompetitionMetrics> GetMetricsAsync(int competitionId)
        {
            var competition = await Db.Competitions
                .AsNoTracking()
                .SingleAsync(item => item.Id == competitionId);
            return (await CompetitionMetricsCalculator.CalculateAsync(
                Db,
                new[] { competition }))[competitionId];
        }

        public CompetitionsModel CreateAdministrationModel(int userId)
        {
            var context = CreateHttpContext(userId);
            return new CompetitionsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                PageContext = new PageContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new TestTempDataProvider())
            };
        }

        public PublicModel CreatePublicModel()
        {
            var context = new DefaultHttpContext();
            return new PublicModel(Db)
            {
                PageContext = new PageContext { HttpContext = context }
            };
        }

        public ResultsModel CreateResultsModel(int userId)
        {
            var context = CreateHttpContext(userId);
            return new ResultsModel(Db)
            {
                PageContext = new PageContext { HttpContext = context }
            };
        }

        public DetailsModel CreateDetailsModel(int userId, int competitionId)
        {
            var context = CreateHttpContext(userId);
            return new DetailsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                Id = competitionId,
                PageContext = new PageContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new TestTempDataProvider())
            };
        }

        public async Task<string> CaptureAsync()
        {
            return string.Join(
                "|",
                string.Join(",", await Db.Competitions.OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.Status}").ToArrayAsync()),
                await Db.Rounds.CountAsync(),
                await Db.Scores.CountAsync(),
                await Db.TeamRounds.CountAsync(),
                await Db.TeamScores.CountAsync(),
                await Db.MatchPlayRounds.CountAsync(),
                await Db.MatchPlayHoleResults.CountAsync());
        }

        private static DefaultHttpContext CreateHttpContext(int userId)
        {
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "Test"))
            };
        }

        private static Competition CreateCompetition(
            string name,
            int clubId,
            int courseId,
            CompetitionType type,
            CompetitionStatus status)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CourseId = courseId,
                CompetitionType = type,
                Status = status,
                Visibility = CompetitionVisibility.Public,
                IsActive = true
            };
        }

        private static async Task AddIndividualDataAsync(
            AppDbContext db,
            Competition competition,
            string suffix,
            bool completeBoth = false)
        {
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = $"Squad {suffix}"
            };
            var players = new[]
            {
                new Player { FirstName = $"{suffix}1", LastName = "Player" },
                new Player { FirstName = $"{suffix}2", LastName = "Player" }
            };
            db.Squads.Add(squad);
            db.Players.AddRange(players);
            await db.SaveChangesAsync();

            var rounds = players.Select(player => new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id
            }).ToArray();
            db.Rounds.AddRange(rounds);
            await db.SaveChangesAsync();

            db.Scores.AddRange(Enumerable.Range(1, 18).Select(hole => new Score
            {
                RoundId = rounds[0].Id,
                HoleNumber = hole,
                Strokes = 4
            }));
            if (completeBoth)
            {
                db.Scores.AddRange(Enumerable.Range(1, 18).Select(hole => new Score
                {
                    RoundId = rounds[1].Id,
                    HoleNumber = hole,
                    Strokes = 5
                }));
            }
            await db.SaveChangesAsync();
        }

        private static async Task AddDoublesDataAsync(
            AppDbContext db,
            Competition competition)
        {
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = "Doubles squad"
            };
            var players = Enumerable.Range(1, 4)
                .Select(index => new Player
                {
                    FirstName = $"D{index}",
                    LastName = "Player"
                })
                .ToArray();
            db.Squads.Add(squad);
            db.Players.AddRange(players);
            await db.SaveChangesAsync();

            db.Rounds.AddRange(players.Select(player => new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id
            }));
            var teams = new[]
            {
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = "Doubles A"
                },
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = "Doubles B"
                }
            };
            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = players[0].Id, Order = 1 },
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = players[1].Id, Order = 2 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = players[2].Id, Order = 1 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = players[3].Id, Order = 2 });
            var teamRounds = teams.Select(team => new TeamRound
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                TeamId = team.Id
            }).ToArray();
            db.TeamRounds.AddRange(teamRounds);
            await db.SaveChangesAsync();

            db.TeamScores.AddRange(Enumerable.Range(1, 18).Select(hole => new TeamScore
            {
                TeamRoundId = teamRounds[0].Id,
                HoleNumber = hole,
                Strokes = 4
            }));
            db.TeamScores.Add(new TeamScore
            {
                TeamRoundId = teamRounds[1].Id,
                HoleNumber = 1,
                Strokes = 5
            });
            await db.SaveChangesAsync();
        }

        private static async Task AddMatchPlayDataAsync(
            AppDbContext db,
            Competition competition)
        {
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = "Match squad"
            };
            var players = new[]
            {
                new Player { FirstName = "M1", LastName = "Player" },
                new Player { FirstName = "M2", LastName = "Player" }
            };
            db.Squads.Add(squad);
            db.Players.AddRange(players);
            await db.SaveChangesAsync();

            db.Rounds.AddRange(players.Select(player => new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id
            }));
            var teams = players.Select((player, index) => new Team
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                Name = $"Match {index + 1}"
            }).ToArray();
            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = players[0].Id, Order = 1 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = players[1].Id, Order = 1 });
            var matches = new[]
            {
                new MatchPlayRound
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    TeamAId = teams[0].Id,
                    TeamBId = teams[1].Id,
                    IsFinished = true,
                    ResultText = "2&1"
                },
                new MatchPlayRound
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    TeamAId = teams[0].Id,
                    TeamBId = teams[1].Id
                }
            };
            db.MatchPlayRounds.AddRange(matches);
            await db.SaveChangesAsync();

            db.MatchPlayHoleResults.Add(new MatchPlayHoleResult
            {
                MatchPlayRoundId = matches[1].Id,
                HoleNumber = 1,
                TeamAScore = 4,
                TeamBScore = 5
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
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
