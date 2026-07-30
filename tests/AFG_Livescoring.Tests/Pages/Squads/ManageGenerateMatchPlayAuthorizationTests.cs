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

public class ManageGenerateMatchPlayAuthorizationTests
{
    [Fact]
    public async Task ClubFromAnotherClub_IsForbiddenWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionBId,
            fixture.SquadBId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task MissingCompetition_IsForbiddenWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            int.MaxValue,
            fixture.SquadAId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task NonMatchPlayFormat_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.IndividualStrokePlay);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task MissingSquad_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            int.MaxValue);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task SquadFromAnotherCompetition_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadBId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task InvalidIndividualRoundCount_IsRejectedWithoutChanges(int roundCount)
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync(roundCountA: roundCount);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task MissingPlayerNavigation_IsRejectedWithoutException()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var missingPlayerId = fixture.PlayerAIds[0];
        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Players WHERE Id = {missingPlayerId}");
        fixture.Db.ChangeTracker.Clear();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task DuplicatePlayerInRounds_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var round = await fixture.Db.Rounds
            .Where(r => r.CompetitionId == fixture.CompetitionAId)
            .OrderBy(r => r.Id)
            .Skip(1)
            .FirstAsync();
        round.PlayerId = fixture.PlayerAIds[0];
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task ValidIndividualGeneration_PreservesHistoricalPairOrderAndOtherCompetition()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        var otherBefore = await fixture.CaptureCompetitionAsync(fixture.CompetitionBId);

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        var teams = await fixture.Db.Teams
            .Where(t => t.CompetitionId == fixture.CompetitionAId && t.SquadId == fixture.SquadAId)
            .OrderBy(t => t.Id)
            .ToListAsync();
        var teamIds = teams.Select(t => t.Id).ToList();
        var teamPlayers = await fixture.Db.TeamPlayers
            .Where(tp => teamIds.Contains(tp.TeamId))
            .ToListAsync();
        var teamRounds = await fixture.Db.TeamRounds
            .Where(tr => tr.CompetitionId == fixture.CompetitionAId && tr.SquadId == fixture.SquadAId)
            .ToListAsync();
        var matches = await fixture.Db.MatchPlayRounds
            .Where(m => m.CompetitionId == fixture.CompetitionAId && m.SquadId == fixture.SquadAId)
            .OrderBy(m => m.Id)
            .ToListAsync();

        Assert.Equal(4, teams.Count);
        Assert.Equal(4, teamPlayers.Count);
        Assert.Equal(4, teamRounds.Count);
        Assert.Equal(2, matches.Count);
        Assert.All(teamPlayers.GroupBy(tp => tp.TeamId), group => Assert.Single(group));
        var playerByTeamId = teamPlayers.ToDictionary(tp => tp.TeamId, tp => tp.PlayerId);
        Assert.Contains(
            matches,
            match => playerByTeamId[match.TeamAId] == fixture.PlayerAIds[0]
                     && playerByTeamId[match.TeamBId] == fixture.PlayerAIds[1]);
        Assert.Contains(
            matches,
            match => playerByTeamId[match.TeamAId] == fixture.PlayerAIds[2]
                     && playerByTeamId[match.TeamBId] == fixture.PlayerAIds[3]);
        Assert.Equal(otherBefore, await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
    }

    [Fact]
    public async Task ValidExistingDoublesTeams_CreateMissingTeamRoundsAndOneMatch()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        var teams = await fixture.AddTwoValidTeamsAsync(addTeamRounds: false);
        var teamIds = teams.Select(t => t.Id).ToArray();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            teamIds,
            await fixture.Db.Teams
                .Where(t => t.CompetitionId == fixture.CompetitionAId)
                .OrderBy(t => t.Id)
                .Select(t => t.Id)
                .ToArrayAsync());
        Assert.Equal(4, await fixture.Db.TeamPlayers.CountAsync(tp => teamIds.Contains(tp.TeamId)));
        Assert.Equal(
            2,
            await fixture.Db.TeamRounds.CountAsync(
                tr => tr.CompetitionId == fixture.CompetitionAId && tr.SquadId == fixture.SquadAId));
        Assert.Single(await fixture.Db.MatchPlayRounds
            .Where(m => m.CompetitionId == fixture.CompetitionAId && m.SquadId == fixture.SquadAId)
            .ToListAsync());
    }

    [Fact]
    public async Task ExistingDoublesTeamWithWrongPlayerCount_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFoursome);
        var teams = await fixture.AddTwoValidTeamsAsync();
        var teamPlayer = await fixture.Db.TeamPlayers.FirstAsync(tp => tp.TeamId == teams[0].Id);
        fixture.Db.TeamPlayers.Remove(teamPlayer);
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task ExistingDoublesTeamWithExternalPlayer_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayScramble);
        var teams = await fixture.AddTwoValidTeamsAsync();
        var teamPlayer = await fixture.Db.TeamPlayers.FirstAsync(tp => tp.TeamId == teams[1].Id);
        teamPlayer.PlayerId = fixture.ExternalPlayerId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task PlayerPresentInBothDoublesTeams_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        var teams = await fixture.AddTwoValidTeamsAsync();
        var teamPlayer = await fixture.Db.TeamPlayers
            .Where(tp => tp.TeamId == teams[1].Id)
            .OrderBy(tp => tp.Order)
            .FirstAsync();
        teamPlayer.PlayerId = fixture.PlayerAIds[0];
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task OutOfScopeTeamRound_IsRejectedWithoutChanges()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        var teams = await fixture.AddTwoValidTeamsAsync();
        fixture.Db.TeamRounds.Add(new TeamRound
        {
            CompetitionId = fixture.CompetitionBId,
            SquadId = fixture.SquadBId,
            TeamId = teams[0].Id
        });
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task AutomaticDoublesWithInvalidRoundCount_IsRejectedWithoutChanges(int roundCount)
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync(roundCountA: roundCount);
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task ValidAutomaticDoubles_ReplacesOnlyTargetStructure()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        await fixture.AddSingleOldTeamAsync();
        var otherBefore = await fixture.CaptureCompetitionAsync(fixture.CompetitionBId);

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        var teams = await fixture.Db.Teams
            .Where(t => t.CompetitionId == fixture.CompetitionAId && t.SquadId == fixture.SquadAId)
            .OrderBy(t => t.Id)
            .ToListAsync();
        var teamIds = teams.Select(t => t.Id).ToList();
        var teamPlayers = await fixture.Db.TeamPlayers
            .Where(tp => teamIds.Contains(tp.TeamId))
            .ToListAsync();

        Assert.Equal(2, teams.Count);
        Assert.Equal(4, teamPlayers.Count);
        Assert.All(
            teamPlayers.GroupBy(tp => tp.TeamId),
            group => Assert.Equal(2, group.Select(tp => tp.PlayerId).Distinct().Count()));
        var teamByName = teams.ToDictionary(team => team.Name);
        var firstTeam = teamByName["A1 Player / A2 Player"];
        var secondTeam = teamByName["A3 Player / A4 Player"];
        Assert.Equal(
            fixture.PlayerAIds.Take(2),
            teamPlayers
                .Where(tp => tp.TeamId == firstTeam.Id)
                .OrderBy(tp => tp.Order)
                .Select(tp => tp.PlayerId));
        Assert.Equal(
            fixture.PlayerAIds.Skip(2),
            teamPlayers
                .Where(tp => tp.TeamId == secondTeam.Id)
                .OrderBy(tp => tp.Order)
                .Select(tp => tp.PlayerId));
        Assert.Equal(
            2,
            await fixture.Db.TeamRounds.CountAsync(
                tr => tr.CompetitionId == fixture.CompetitionAId && tr.SquadId == fixture.SquadAId));
        Assert.Single(await fixture.Db.MatchPlayRounds
            .Where(m => m.CompetitionId == fixture.CompetitionAId && m.SquadId == fixture.SquadAId)
            .ToListAsync());
        Assert.Equal(otherBefore, await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
    }

    [Fact]
    public async Task ExistingMatchWithoutResult_PreservesStructure()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.AddExistingMatchAsync(addHoleResult: false);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task ExistingMatchPlayHoleResult_PreservesStructure()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.AddExistingMatchAsync(addHoleResult: true);
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.Single(await fixture.Db.MatchPlayHoleResults.ToListAsync());
    }

    [Fact]
    public async Task ExistingTeamScore_PreservesCompleteStructure()
    {
        await using var fixture = await GenerateMatchPlayFixture.CreateAsync();
        await fixture.SetCompetitionTypeAsync(CompetitionType.MatchPlayFourball);
        var teams = await fixture.AddTwoValidTeamsAsync(addTeamRounds: true);
        var teamRound = await fixture.Db.TeamRounds.FirstAsync(tr => tr.TeamId == teams[0].Id);
        fixture.Db.TeamScores.Add(new TeamScore
        {
            TeamRoundId = teamRound.Id,
            HoleNumber = 1,
            Strokes = 4
        });
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel().OnPostGenerateMatchPlayAsync(
            fixture.CompetitionAId,
            fixture.SquadAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.Single(await fixture.Db.TeamScores.ToListAsync());
    }

    private sealed class GenerateMatchPlayFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private GenerateMatchPlayFixture(
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

        public static async Task<GenerateMatchPlayFixture> CreateAsync(int roundCountA = 4)
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
                Email = "generate-match-play@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var competitionA = CreateCompetition("Competition A", clubA.Id);
            var competitionB = CreateCompetition("Competition B", clubB.Id);
            db.AppUsers.Add(user);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            var squadA = new Squad { CompetitionId = competitionA.Id, Name = "Squad A" };
            var squadB = new Squad { CompetitionId = competitionB.Id, Name = "Squad B" };
            db.Squads.AddRange(squadA, squadB);
            await db.SaveChangesAsync();

            var playersA = CreatePlayers("A", Math.Max(roundCountA, 4));
            var playersB = CreatePlayers("B", 4);
            var external = new Player { FirstName = "External", LastName = "Player" };
            db.Players.AddRange(playersA);
            db.Players.AddRange(playersB);
            db.Players.Add(external);
            await db.SaveChangesAsync();

            db.Rounds.AddRange(playersA.Take(roundCountA).Select(player => new Round
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

            return new GenerateMatchPlayFixture(
                connection,
                db,
                user.Id,
                competitionA.Id,
                competitionB.Id,
                squadA.Id,
                squadB.Id,
                playersA.Take(roundCountA).Select(player => player.Id).ToArray(),
                playersB.Select(player => player.Id).ToArray(),
                external.Id);
        }

        public ManageModel CreateModel()
        {
            var httpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(UserId)
            };

            return new ManageModel(Db, new CompetitionAuthorizationService(Db))
            {
                PageContext = new PageContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        public async Task SetCompetitionTypeAsync(CompetitionType type)
        {
            var competition = await Db.Competitions.FindAsync(CompetitionAId);
            competition!.CompetitionType = type;
            await Db.SaveChangesAsync();
        }

        public async Task<Team[]> AddTwoValidTeamsAsync(bool addTeamRounds = true)
        {
            var teams = new[]
            {
                new Team
                {
                    CompetitionId = CompetitionAId,
                    SquadId = SquadAId,
                    Name = "Team A",
                    IsActive = true
                },
                new Team
                {
                    CompetitionId = CompetitionAId,
                    SquadId = SquadAId,
                    Name = "Team B",
                    IsActive = true
                }
            };
            Db.Teams.AddRange(teams);
            await Db.SaveChangesAsync();

            Db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = PlayerAIds[0], Order = 1 },
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = PlayerAIds[1], Order = 2 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = PlayerAIds[2], Order = 1 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = PlayerAIds[3], Order = 2 });

            if (addTeamRounds)
            {
                Db.TeamRounds.AddRange(
                    new TeamRound
                    {
                        CompetitionId = CompetitionAId,
                        SquadId = SquadAId,
                        TeamId = teams[0].Id
                    },
                    new TeamRound
                    {
                        CompetitionId = CompetitionAId,
                        SquadId = SquadAId,
                        TeamId = teams[1].Id
                    });
            }

            await Db.SaveChangesAsync();
            return teams;
        }

        public async Task AddSingleOldTeamAsync()
        {
            var team = new Team
            {
                CompetitionId = CompetitionAId,
                SquadId = SquadAId,
                Name = "Old team",
                IsActive = true
            };
            Db.Teams.Add(team);
            await Db.SaveChangesAsync();
            Db.TeamPlayers.Add(new TeamPlayer
            {
                TeamId = team.Id,
                PlayerId = PlayerAIds[0],
                Order = 1
            });
            Db.TeamRounds.Add(new TeamRound
            {
                CompetitionId = CompetitionAId,
                SquadId = SquadAId,
                TeamId = team.Id
            });
            await Db.SaveChangesAsync();
        }

        public async Task AddExistingMatchAsync(bool addHoleResult)
        {
            var teams = await AddTwoValidTeamsAsync();
            var match = new MatchPlayRound
            {
                CompetitionId = CompetitionAId,
                SquadId = SquadAId,
                TeamAId = teams[0].Id,
                TeamBId = teams[1].Id
            };
            Db.MatchPlayRounds.Add(match);
            await Db.SaveChangesAsync();

            if (addHoleResult)
            {
                Db.MatchPlayHoleResults.Add(new MatchPlayHoleResult
                {
                    MatchPlayRoundId = match.Id,
                    HoleNumber = 1,
                    TeamAScore = 4,
                    TeamBScore = 5
                });
                await Db.SaveChangesAsync();
            }
        }

        public async Task<StructureSnapshot> CaptureAsync()
        {
            Db.ChangeTracker.DetectChanges();
            return new StructureSnapshot(
                string.Join(",", await Db.Teams.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamPlayers.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamRounds.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()),
                string.Join(",", await Db.TeamScores.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()),
                string.Join(",", await Db.MatchPlayRounds.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()),
                string.Join(",", await Db.MatchPlayHoleResults.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()));
        }

        public async Task<string> CaptureCompetitionAsync(int competitionId)
        {
            var teamIds = await Db.Teams
                .Where(t => t.CompetitionId == competitionId)
                .OrderBy(t => t.Id)
                .Select(t => t.Id)
                .ToListAsync();
            return string.Join("|",
                string.Join(",", teamIds),
                string.Join(",", await Db.TeamPlayers
                    .Where(tp => teamIds.Contains(tp.TeamId))
                    .OrderBy(tp => tp.Id)
                    .Select(tp => tp.Id)
                    .ToArrayAsync()),
                string.Join(",", await Db.TeamRounds
                    .Where(tr => tr.CompetitionId == competitionId)
                    .OrderBy(tr => tr.Id)
                    .Select(tr => tr.Id)
                    .ToArrayAsync()),
                string.Join(",", await Db.MatchPlayRounds
                    .Where(m => m.CompetitionId == competitionId)
                    .OrderBy(m => m.Id)
                    .Select(m => m.Id)
                    .ToArrayAsync()));
        }

        private static Competition CreateCompetition(string name, int clubId)
        {
            return new Competition
            {
                Name = name,
                ClubId = clubId,
                CompetitionType = CompetitionType.MatchPlayIndividual
            };
        }

        private static Player[] CreatePlayers(string prefix, int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => new Player
                {
                    FirstName = $"{prefix}{index}",
                    LastName = "Player",
                    IsActive = true
                })
                .ToArray();
        }

        private static ClaimsPrincipal CreatePrincipal(int userId)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test"));
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
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
