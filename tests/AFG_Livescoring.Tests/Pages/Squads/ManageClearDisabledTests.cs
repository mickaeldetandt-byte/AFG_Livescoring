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

public class ManageClearDisabledTests
{
    [Fact]
    public async Task ClubFromAnotherClub_IsForbiddenWithoutChanges()
    {
        await using var fixture = await ClearFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel();

        var result = await model.OnPostClearAsync(fixture.CompetitionBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task MissingCompetition_IsForbiddenWithoutChanges()
    {
        await using var fixture = await ClearFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel();

        var result = await model.OnPostClearAsync(int.MaxValue);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OwnerWithCompleteScoredStructure_IsRedirectedWithoutAnyChanges()
    {
        await using var fixture = await ClearFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var otherCompetitionBefore = await fixture.CaptureCompetitionAsync(fixture.CompetitionBId);
        var model = fixture.CreateModel();

        var result = await model.OnPostClearAsync(fixture.CompetitionAId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(fixture.CompetitionAId, redirect.RouteValues!["competitionId"]);
        Assert.Equal(
            "La réinitialisation globale est désactivée afin de protéger les scores et les résultats de la compétition.",
            model.TempData["Message"]);
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.Equal(
            otherCompetitionBefore,
            await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
    }

    [Fact]
    public async Task TrainingCompetition_PreservesScoresRoundStateAndSquads()
    {
        await using var fixture = await ClearFixture.CreateAsync(trainingA: true);
        var before = await fixture.CaptureAsync();
        var roundBefore = await fixture.Db.Rounds
            .AsNoTracking()
            .OrderBy(round => round.Id)
            .FirstAsync(round => round.CompetitionId == fixture.CompetitionAId);
        var squadCountBefore = await fixture.Db.Squads
            .CountAsync(squad => squad.CompetitionId == fixture.CompetitionAId);
        var scoreCountBefore = await fixture.Db.Scores
            .CountAsync(score => score.Round!.CompetitionId == fixture.CompetitionAId);
        var model = fixture.CreateModel();

        var result = await model.OnPostClearAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        fixture.Db.ChangeTracker.Clear();
        var roundAfter = await fixture.Db.Rounds
            .AsNoTracking()
            .SingleAsync(round => round.Id == roundBefore.Id);
        Assert.Equal(roundBefore.SquadId, roundAfter.SquadId);
        Assert.Equal(roundBefore.IsLocked, roundAfter.IsLocked);
        Assert.Equal(
            squadCountBefore,
            await fixture.Db.Squads.CountAsync(
                squad => squad.CompetitionId == fixture.CompetitionAId));
        Assert.Equal(
            scoreCountBefore,
            await fixture.Db.Scores.CountAsync(
                score => score.Round!.CompetitionId == fixture.CompetitionAId));
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    private sealed class ClearFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ClearFixture(
            SqliteConnection connection,
            AppDbContext db,
            int userId,
            int competitionAId,
            int competitionBId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
        }

        public AppDbContext Db { get; }
        public int UserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }

        public static async Task<ClearFixture> CreateAsync(bool trainingA = false)
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
                Email = "clear-disabled@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competitionA = new Competition
            {
                Name = "Competition A",
                ClubId = clubA.Id,
                CompetitionType = CompetitionType.MatchPlayIndividual,
                Mode = trainingA ? "Training" : "Competition"
            };
            var competitionB = new Competition
            {
                Name = "Competition B",
                ClubId = clubB.Id,
                CompetitionType = CompetitionType.MatchPlayIndividual
            };
            db.AppUsers.Add(user);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            await AddCompleteStructureAsync(db, competitionA, "A", roundIsLocked: true);
            await AddCompleteStructureAsync(db, competitionB, "B", roundIsLocked: true);

            return new ClearFixture(
                connection,
                db,
                user.Id,
                competitionA.Id,
                competitionB.Id);
        }

        public ManageModel CreateModel()
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) },
                    authenticationType: "Test"))
            };

            return new ManageModel(Db, new CompetitionAuthorizationService(Db))
            {
                PageContext = new PageContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        public async Task<DatabaseSnapshot> CaptureAsync()
        {
            Db.ChangeTracker.DetectChanges();

            return new DatabaseSnapshot(
                string.Join(",", await Db.Squads
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.Name}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Rounds
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.PlayerId}:{item.SquadId}:{item.IsLocked}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Scores
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.RoundId}:{item.HoleNumber}:{item.Strokes}")
                    .ToArrayAsync()),
                string.Join(",", await Db.Teams
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.SquadId}:{item.Name}:{item.IsActive}")
                    .ToArrayAsync()),
                string.Join(",", await Db.TeamPlayers
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.TeamId}:{item.PlayerId}:{item.Order}")
                    .ToArrayAsync()),
                string.Join(",", await Db.TeamRounds
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.CompetitionId}:{item.TeamId}:{item.SquadId}:{item.IsLocked}")
                    .ToArrayAsync()),
                string.Join(",", await Db.TeamScores
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}:{item.TeamRoundId}:{item.HoleNumber}:{item.Strokes}")
                    .ToArrayAsync()),
                string.Join(",", await Db.MatchPlayRounds
                    .OrderBy(item => item.Id)
                    .Select(item =>
                        $"{item.Id}:{item.CompetitionId}:{item.SquadId}:{item.TeamAId}:{item.TeamBId}:{item.CurrentHole}:{item.IsFinished}")
                    .ToArrayAsync()),
                string.Join(",", await Db.MatchPlayHoleResults
                    .OrderBy(item => item.Id)
                    .Select(item =>
                        $"{item.Id}:{item.MatchPlayRoundId}:{item.HoleNumber}:{item.TeamAScore}:{item.TeamBScore}")
                    .ToArrayAsync()));
        }

        public async Task<string> CaptureCompetitionAsync(int competitionId)
        {
            var squadIds = await Db.Squads
                .Where(squad => squad.CompetitionId == competitionId)
                .OrderBy(squad => squad.Id)
                .Select(squad => squad.Id)
                .ToArrayAsync();
            var roundIds = await Db.Rounds
                .Where(round => round.CompetitionId == competitionId)
                .OrderBy(round => round.Id)
                .Select(round => round.Id)
                .ToArrayAsync();
            var teamIds = await Db.Teams
                .Where(team => team.CompetitionId == competitionId)
                .OrderBy(team => team.Id)
                .Select(team => team.Id)
                .ToArrayAsync();
            var matchIds = await Db.MatchPlayRounds
                .Where(match => match.CompetitionId == competitionId)
                .OrderBy(match => match.Id)
                .Select(match => match.Id)
                .ToArrayAsync();

            return string.Join(
                "|",
                string.Join(",", squadIds),
                string.Join(",", roundIds),
                string.Join(",", teamIds),
                string.Join(",", matchIds));
        }

        private static async Task AddCompleteStructureAsync(
            AppDbContext db,
            Competition competition,
            string prefix,
            bool roundIsLocked)
        {
            var squad = new Squad
            {
                CompetitionId = competition.Id,
                Name = $"Squad {prefix}"
            };
            var players = new[]
            {
                new Player { FirstName = $"{prefix}1", LastName = "Player" },
                new Player { FirstName = $"{prefix}2", LastName = "Player" }
            };
            db.Squads.Add(squad);
            db.Players.AddRange(players);
            await db.SaveChangesAsync();

            var rounds = players.Select(player => new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = player.Id,
                IsLocked = roundIsLocked
            }).ToArray();
            db.Rounds.AddRange(rounds);
            await db.SaveChangesAsync();

            db.Scores.Add(new Score
            {
                RoundId = rounds[0].Id,
                HoleNumber = 1,
                Strokes = 4
            });

            var teams = new[]
            {
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = $"Team {prefix}1"
                },
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = $"Team {prefix}2"
                }
            };
            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = players[0].Id, Order = 1 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = players[1].Id, Order = 1 });

            var teamRounds = new[]
            {
                new TeamRound
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    TeamId = teams[0].Id
                },
                new TeamRound
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    TeamId = teams[1].Id
                }
            };
            db.TeamRounds.AddRange(teamRounds);
            await db.SaveChangesAsync();

            db.TeamScores.Add(new TeamScore
            {
                TeamRoundId = teamRounds[0].Id,
                HoleNumber = 1,
                Strokes = 4
            });

            var match = new MatchPlayRound
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                TeamAId = teams[0].Id,
                TeamBId = teams[1].Id,
                CurrentHole = 2,
                StatusText = "1UP"
            };
            db.MatchPlayRounds.Add(match);
            await db.SaveChangesAsync();

            db.MatchPlayHoleResults.Add(new MatchPlayHoleResult
            {
                MatchPlayRoundId = match.Id,
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

    private sealed record DatabaseSnapshot(
        string Squads,
        string Rounds,
        string Scores,
        string Teams,
        string TeamPlayers,
        string TeamRounds,
        string TeamScores,
        string MatchPlayRounds,
        string MatchPlayHoleResults);

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
