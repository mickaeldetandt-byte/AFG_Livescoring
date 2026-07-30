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
using Xunit;

namespace AFG_Livescoring.Tests.Pages.Competitions;

public sealed class CompetitionDeleteAuthorizationTests
{
    [Fact]
    public async Task Admin_can_delete_an_empty_competition()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        var otherBefore = await fixture.CaptureCompetitionAsync(fixture.CompetitionBId);

        var result = await fixture.CreateModel(fixture.AdminUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(await fixture.Db.Competitions.AnyAsync(
            competition => competition.Id == fixture.CompetitionAId));
        Assert.Equal(otherBefore, await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
        Assert.Equal(0, await fixture.CountStructureAsync(fixture.CompetitionAId));
    }

    [Fact]
    public async Task Owning_club_can_delete_its_empty_competition()
    {
        await using var fixture = await DeleteFixture.CreateAsync();

        var result = await fixture.CreateModel(fixture.ClubAUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(await fixture.Db.Competitions.AnyAsync(
            competition => competition.Id == fixture.CompetitionAId));
    }

    [Fact]
    public async Task Another_club_is_forbidden_without_any_write()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.ClubBUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Missing_competition_redirects_without_any_write()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.AdminUserId)
            .OnPostDeleteAsync(int.MaxValue);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Individual_score_blocks_deletion_without_any_write()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        fixture.Db.Scores.Add(new Score
        {
            RoundId = fixture.RoundAId,
            HoleNumber = 1,
            Strokes = 4
        });
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.ClubAUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.True(await fixture.Db.Competitions.AnyAsync(
            competition => competition.Id == fixture.CompetitionAId));
    }

    [Fact]
    public async Task Team_score_blocks_deletion_without_any_write()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        fixture.Db.TeamScores.Add(new TeamScore
        {
            TeamRoundId = fixture.TeamRoundAId,
            HoleNumber = 1,
            Strokes = 4
        });
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.ClubAUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task Match_play_hole_result_blocks_deletion_without_any_write()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        fixture.Db.MatchPlayHoleResults.Add(new MatchPlayHoleResult
        {
            MatchPlayRoundId = fixture.MatchPlayRoundAId,
            HoleNumber = 1,
            TeamAScore = 4,
            TeamBScore = 5
        });
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.CaptureAsync();

        var result = await fixture.CreateModel(fixture.ClubAUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(before, await fixture.CaptureAsync());
    }

    [Fact]
    public async Task A_save_failure_rolls_back_the_complete_deletion()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        var before = await fixture.CaptureAsync();
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
             CREATE TRIGGER RejectCompetitionDelete
             BEFORE DELETE ON Competitions
             WHEN OLD.Name = 'Competition A'
             BEGIN
                 SELECT RAISE(ABORT, 'forced deletion failure');
             END;
             """);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => fixture.CreateModel(fixture.AdminUserId)
                .OnPostDeleteAsync(fixture.CompetitionAId));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(before, await fixture.CaptureAsync());
        Assert.True(await fixture.Db.Competitions.AnyAsync(
            competition => competition.Id == fixture.CompetitionAId));
    }

    [Fact]
    public async Task Deleting_one_competition_never_changes_another_competition()
    {
        await using var fixture = await DeleteFixture.CreateAsync();
        var otherBefore = await fixture.CaptureCompetitionAsync(fixture.CompetitionBId);

        await fixture.CreateModel(fixture.AdminUserId)
            .OnPostDeleteAsync(fixture.CompetitionAId);

        Assert.Equal(otherBefore, await fixture.CaptureCompetitionAsync(fixture.CompetitionBId));
        Assert.True(await fixture.Db.Competitions.AnyAsync(
            competition => competition.Id == fixture.CompetitionBId));
    }

    private sealed class DeleteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DeleteFixture(
            SqliteConnection connection,
            AppDbContext db,
            int adminUserId,
            int clubAUserId,
            int clubBUserId,
            int competitionAId,
            int competitionBId,
            int roundAId,
            int teamRoundAId,
            int matchPlayRoundAId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClubAUserId = clubAUserId;
            ClubBUserId = clubBUserId;
            CompetitionAId = competitionAId;
            CompetitionBId = competitionBId;
            RoundAId = roundAId;
            TeamRoundAId = teamRoundAId;
            MatchPlayRoundAId = matchPlayRoundAId;
        }

        public AppDbContext Db { get; }
        public int AdminUserId { get; }
        public int ClubAUserId { get; }
        public int ClubBUserId { get; }
        public int CompetitionAId { get; }
        public int CompetitionBId { get; }
        public int RoundAId { get; }
        public int TeamRoundAId { get; }
        public int MatchPlayRoundAId { get; }

        public static async Task<DeleteFixture> CreateAsync()
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

            var admin = new AppUser { Email = "admin@example.invalid", Role = "Admin" };
            var clubAUser = new AppUser
            {
                Email = "club-a@example.invalid",
                Role = "Club",
                ClubId = clubA.Id
            };
            var clubBUser = new AppUser
            {
                Email = "club-b@example.invalid",
                Role = "Club",
                ClubId = clubB.Id
            };
            var competitionA = new Competition
            {
                Name = "Competition A",
                ClubId = clubA.Id,
                CompetitionType = CompetitionType.MatchPlayIndividual
            };
            var competitionB = new Competition
            {
                Name = "Competition B",
                ClubId = clubB.Id,
                CompetitionType = CompetitionType.MatchPlayIndividual
            };
            db.AppUsers.AddRange(admin, clubAUser, clubBUser);
            db.Competitions.AddRange(competitionA, competitionB);
            await db.SaveChangesAsync();

            var structureA = await AddUnscoredStructureAsync(db, competitionA, "A");
            await AddUnscoredStructureAsync(db, competitionB, "B");

            return new DeleteFixture(
                connection,
                db,
                admin.Id,
                clubAUser.Id,
                clubBUser.Id,
                competitionA.Id,
                competitionB.Id,
                structureA.RoundId,
                structureA.TeamRoundId,
                structureA.MatchPlayRoundId);
        }

        public CompetitionsModel CreateModel(int userId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "Test"))
            };

            return new CompetitionsModel(
                Db,
                new CompetitionAuthorizationService(Db))
            {
                PageContext = new PageContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        public async Task<int> CountStructureAsync(int competitionId)
        {
            return await Db.Rounds.CountAsync(item => item.CompetitionId == competitionId)
                   + await Db.Squads.CountAsync(item => item.CompetitionId == competitionId)
                   + await Db.Teams.CountAsync(item => item.CompetitionId == competitionId)
                   + await Db.TeamRounds.CountAsync(item => item.CompetitionId == competitionId)
                   + await Db.MatchPlayRounds.CountAsync(item => item.CompetitionId == competitionId);
        }

        public async Task<string> CaptureCompetitionAsync(int competitionId)
        {
            return string.Join(
                "|",
                await Db.Competitions.CountAsync(item => item.Id == competitionId),
                await Db.Squads.CountAsync(item => item.CompetitionId == competitionId),
                await Db.Rounds.CountAsync(item => item.CompetitionId == competitionId),
                await Db.Teams.CountAsync(item => item.CompetitionId == competitionId),
                await Db.TeamRounds.CountAsync(item => item.CompetitionId == competitionId),
                await Db.MatchPlayRounds.CountAsync(item => item.CompetitionId == competitionId));
        }

        public async Task<DatabaseSnapshot> CaptureAsync()
        {
            return new DatabaseSnapshot(
                string.Join(",", await Db.Competitions.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.ClubId}:{x.Name}").ToArrayAsync()),
                string.Join(",", await Db.Squads.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.CompetitionId}").ToArrayAsync()),
                string.Join(",", await Db.Rounds.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.CompetitionId}:{x.SquadId}").ToArrayAsync()),
                string.Join(",", await Db.Scores.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.RoundId}:{x.HoleNumber}:{x.Strokes}").ToArrayAsync()),
                string.Join(",", await Db.Teams.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.CompetitionId}:{x.SquadId}").ToArrayAsync()),
                string.Join(",", await Db.TeamPlayers.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.TeamId}:{x.PlayerId}:{x.Order}").ToArrayAsync()),
                string.Join(",", await Db.TeamRounds.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.CompetitionId}:{x.TeamId}:{x.SquadId}").ToArrayAsync()),
                string.Join(",", await Db.TeamScores.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.TeamRoundId}:{x.HoleNumber}:{x.Strokes}").ToArrayAsync()),
                string.Join(",", await Db.MatchPlayRounds.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.CompetitionId}:{x.SquadId}:{x.TeamAId}:{x.TeamBId}")
                    .ToArrayAsync()),
                string.Join(",", await Db.MatchPlayHoleResults.OrderBy(x => x.Id)
                    .Select(x => $"{x.Id}:{x.MatchPlayRoundId}:{x.HoleNumber}").ToArrayAsync()));
        }

        private static async Task<StructureIds> AddUnscoredStructureAsync(
            AppDbContext db,
            Competition competition,
            string suffix)
        {
            var squad = new Squad { CompetitionId = competition.Id, Name = $"Squad {suffix}" };
            var players = new[]
            {
                new Player { FirstName = $"{suffix}1", LastName = "Player" },
                new Player { FirstName = $"{suffix}2", LastName = "Player" }
            };
            db.Squads.Add(squad);
            db.Players.AddRange(players);
            await db.SaveChangesAsync();

            var round = new Round
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                PlayerId = players[0].Id
            };
            db.Rounds.Add(round);

            var teams = new[]
            {
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = $"Team {suffix}1"
                },
                new Team
                {
                    CompetitionId = competition.Id,
                    SquadId = squad.Id,
                    Name = $"Team {suffix}2"
                }
            };
            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();

            db.TeamPlayers.AddRange(
                new TeamPlayer { TeamId = teams[0].Id, PlayerId = players[0].Id, Order = 1 },
                new TeamPlayer { TeamId = teams[1].Id, PlayerId = players[1].Id, Order = 1 });

            var teamRound = new TeamRound
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                TeamId = teams[0].Id
            };
            db.TeamRounds.Add(teamRound);

            var match = new MatchPlayRound
            {
                CompetitionId = competition.Id,
                SquadId = squad.Id,
                TeamAId = teams[0].Id,
                TeamBId = teams[1].Id
            };
            db.MatchPlayRounds.Add(match);
            await db.SaveChangesAsync();

            return new StructureIds(round.Id, teamRound.Id, match.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record StructureIds(int RoundId, int TeamRoundId, int MatchPlayRoundId);

    private sealed record DatabaseSnapshot(
        string Competitions,
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

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}
