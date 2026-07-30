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

public class ManageSaveTeamsAuthorizationTests
{
    [Fact]
    public async Task OnPostSaveTeamsAsync_ClubFromAnotherClub_IsForbiddenWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionBId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionBId,
            fixture.SquadBId,
            fixture.PlayerBIds[0],
            fixture.PlayerBIds[1],
            fixture.PlayerBIds[2],
            fixture.PlayerBIds[3]);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_MissingCompetition_IsForbiddenWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(int.MaxValue);

        var result = await model.OnPostSaveTeamsAsync(
            int.MaxValue,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_SquadFromAnotherCompetition_IsRejectedWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadBId,
            fixture.PlayerBIds[0],
            fixture.PlayerBIds[1],
            fixture.PlayerBIds[2],
            fixture.PlayerBIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_MissingSquad_IsRejectedWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            int.MaxValue,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task OnPostSaveTeamsAsync_InvalidRoundCount_IsRejectedWithoutChanges(int roundCount)
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();

        if (roundCount == 3)
        {
            var roundToRemove = await fixture.Db.Rounds
                .FirstAsync(round => round.CompetitionId == fixture.CompetitionAId);
            fixture.Db.Rounds.Remove(roundToRemove);
        }
        else
        {
            var extraPlayer = new Player
            {
                FirstName = "Extra",
                LastName = "Player",
                IsActive = true
            };
            fixture.Db.Rounds.Add(new Round
            {
                CompetitionId = fixture.CompetitionAId,
                SquadId = fixture.SquadAId,
                Player = extraPlayer
            });
        }

        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_PlayerOutsideSquad_IsRejectedWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.ExternalPlayerId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_DuplicatePlayer_IsRejectedWithoutChanges()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_MissingPlayerNavigation_IsRejectedWithoutException()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var missingPlayerId = fixture.PlayerAIds[0];

        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Players WHERE Id = {missingPlayerId}");
        fixture.Db.ChangeTracker.Clear();

        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_ExistingTeamScore_PreservesCompleteStructure()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync(addTeamScoreToA: true);
        var before = await fixture.CaptureAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.Single(await fixture.Db.TeamScores.ToListAsync());
    }

    [Fact]
    public async Task OnPostSaveTeamsAsync_ValidReplacement_ReplacesOnlyAuthorizedSquad()
    {
        await using var fixture = await SaveTeamsFixture.CreateAsync();
        var oldTeamAIds = await fixture.Db.Teams
            .Where(team => team.CompetitionId == fixture.CompetitionAId)
            .Select(team => team.Id)
            .ToListAsync();
        var teamBIds = await fixture.Db.Teams
            .Where(team => team.CompetitionId == fixture.CompetitionBId)
            .Select(team => team.Id)
            .ToListAsync();
        var model = fixture.CreateModel(fixture.CompetitionAId);

        var result = await model.OnPostSaveTeamsAsync(
            fixture.CompetitionAId,
            fixture.SquadAId,
            fixture.PlayerAIds[0],
            fixture.PlayerAIds[2],
            fixture.PlayerAIds[1],
            fixture.PlayerAIds[3]);

        Assert.IsType<RedirectToPageResult>(result);

        var newTeams = await fixture.Db.Teams
            .Where(team => team.CompetitionId == fixture.CompetitionAId
                           && team.SquadId == fixture.SquadAId)
            .ToListAsync();
        var newTeamIds = newTeams.Select(team => team.Id).ToList();
        var newTeamPlayers = await fixture.Db.TeamPlayers
            .Where(teamPlayer => newTeamIds.Contains(teamPlayer.TeamId))
            .ToListAsync();
        var newTeamRounds = await fixture.Db.TeamRounds
            .Where(teamRound => teamRound.CompetitionId == fixture.CompetitionAId
                                && teamRound.SquadId == fixture.SquadAId)
            .ToListAsync();

        Assert.Equal(2, newTeams.Count);
        Assert.DoesNotContain(newTeams, team => oldTeamAIds.Contains(team.Id));
        Assert.Equal(4, newTeamPlayers.Count);
        Assert.Equal(2, newTeamRounds.Count);
        Assert.All(
            newTeamPlayers.GroupBy(teamPlayer => teamPlayer.TeamId),
            team => Assert.Equal(2, team.Select(player => player.PlayerId).Distinct().Count()));
        Assert.Equal(
            fixture.PlayerAIds.OrderBy(id => id),
            newTeamPlayers.Select(teamPlayer => teamPlayer.PlayerId).OrderBy(id => id));

        Assert.All(
            teamBIds,
            teamId => Assert.True(fixture.Db.Teams.Any(team => team.Id == teamId)));
        Assert.Equal(
            4,
            await fixture.Db.TeamPlayers.CountAsync(teamPlayer => teamBIds.Contains(teamPlayer.TeamId)));
        Assert.Equal(
            2,
            await fixture.Db.TeamRounds.CountAsync(
                teamRound => teamRound.CompetitionId == fixture.CompetitionBId
                             && teamRound.SquadId == fixture.SquadBId));
    }

    private sealed class SaveTeamsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SaveTeamsFixture(
            SqliteConnection connection,
            AppDbContext db,
            int userId,
            int competitionAId,
            int competitionBId,
            int squadAId,
            int squadBId,
            int[] playerAIds,
            int[] playerBIds,
            int externalPlayerId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
            SquadAId = squadAId;
            SquadBId = squadBId;
            PlayerAIds = playerAIds;
            PlayerBIds = playerBIds;
            ExternalPlayerId = externalPlayerId;
        }

        public AppDbContext Db { get; }
        public int UserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }
        public int SquadAId { get; }
        public int SquadBId { get; }
        public int[] PlayerAIds { get; }
        public int[] PlayerBIds { get; }
        public int ExternalPlayerId { get; }

        public static async Task<SaveTeamsFixture> CreateAsync(bool addTeamScoreToA = false)
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
                Email = "club-a-save-teams@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competitionA = CreateCompetition("Competition A", clubA.Id);
            var competitionB = CreateCompetition("Competition B", clubB.Id);
            db.AppUsers.Add(user);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            var squadA = new Squad
            {
                CompetitionId = competitionA.Id,
                Name = "Squad A",
                StartHole = 1
            };
            var squadB = new Squad
            {
                CompetitionId = competitionB.Id,
                Name = "Squad B",
                StartHole = 1
            };
            db.Squads.AddRange(squadA, squadB);
            await db.SaveChangesAsync();

            var playersA = CreatePlayers("A");
            var playersB = CreatePlayers("B");
            var externalPlayer = new Player
            {
                FirstName = "External",
                LastName = "Player",
                IsActive = true
            };
            db.Players.AddRange(playersA);
            db.Players.AddRange(playersB);
            db.Players.Add(externalPlayer);
            await db.SaveChangesAsync();

            db.Rounds.AddRange(playersA.Select(player => new Round
            {
                CompetitionId = competitionA.Id,
                SquadId = squadA.Id,
                PlayerId = player.Id
            }));
            db.Rounds.AddRange(playersB.Select(player => new Round
            {
                CompetitionId = competitionB.Id,
                SquadId = squadB.Id,
                PlayerId = player.Id
            }));
            await db.SaveChangesAsync();

            var teamRoundsA = await AddExistingStructureAsync(
                db,
                competitionA.Id,
                squadA.Id,
                playersA);
            await AddExistingStructureAsync(
                db,
                competitionB.Id,
                squadB.Id,
                playersB);

            if (addTeamScoreToA)
            {
                db.TeamScores.Add(new TeamScore
                {
                    TeamRoundId = teamRoundsA[0].Id,
                    HoleNumber = 1,
                    Strokes = 4
                });
                await db.SaveChangesAsync();
            }

            return new SaveTeamsFixture(
                connection,
                db,
                user.Id,
                competitionA.Id,
                competitionB.Id,
                squadA.Id,
                squadB.Id,
                playersA.Select(player => player.Id).ToArray(),
                playersB.Select(player => player.Id).ToArray(),
                externalPlayer.Id);
        }

        public ManageModel CreateModel(int competitionId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(UserId)
            };

            return new ManageModel(Db, new CompetitionAuthorizationService(Db))
            {
                competitionId = competitionId,
                PageContext = new PageContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        public async Task<StructureSnapshot> CaptureAsync()
        {
            Db.ChangeTracker.DetectChanges();

            return new StructureSnapshot(
                string.Join(",", await Db.Teams.OrderBy(team => team.Id).Select(team => team.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamPlayers.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamRounds.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamScores.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync()),
                string.Join(",", await Db.MatchPlayRounds.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync()),
                string.Join(",", await Db.MatchPlayHoleResults.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync()));
        }

        private static Competition CreateCompetition(string name, int clubId)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CompetitionType = CompetitionType.DoublesScramble
            };
        }

        private static Player[] CreatePlayers(string prefix)
        {
            return Enumerable.Range(1, 4)
                .Select(index => new Player
                {
                    FirstName = $"{prefix}{index}",
                    LastName = "Player",
                    IsActive = true
                })
                .ToArray();
        }

        private static async Task<TeamRound[]> AddExistingStructureAsync(
            AppDbContext db,
            int competitionId,
            int squadId,
            IReadOnlyList<Player> players)
        {
            var teamA = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = "Old team A"
            };
            var teamB = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = "Old team B"
            };
            db.Teams.AddRange(teamA, teamB);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teamA.Id, PlayerId = players[0].Id, Order = 1 },
                new TeamPlayer { TeamId = teamA.Id, PlayerId = players[1].Id, Order = 2 },
                new TeamPlayer { TeamId = teamB.Id, PlayerId = players[2].Id, Order = 1 },
                new TeamPlayer { TeamId = teamB.Id, PlayerId = players[3].Id, Order = 2 });

            var teamRoundA = new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamA.Id,
                SquadId = squadId
            };
            var teamRoundB = new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamB.Id,
                SquadId = squadId
            };
            db.TeamRounds.AddRange(teamRoundA, teamRoundB);
            await db.SaveChangesAsync();

            return new[] { teamRoundA, teamRoundB };
        }

        private static ClaimsPrincipal CreatePrincipal(int userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");

            return new ClaimsPrincipal(identity);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record StructureSnapshot(
        string TeamIds,
        string TeamPlayerIds,
        string TeamRoundIds,
        string TeamScoreIds,
        string MatchPlayRoundIds,
        string MatchPlayHoleResultIds);

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
