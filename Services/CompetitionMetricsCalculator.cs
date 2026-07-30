using AFG_Livescoring.Models;
using Microsoft.EntityFrameworkCore;

namespace AFG_Livescoring.Services;

public sealed record CompetitionMetrics(
    int CompetitionId,
    int ParticipantsCount,
    int TotalRounds,
    int CompletedRounds,
    int StartedRounds,
    bool HasStarted,
    bool IsFinished);

public static class CompetitionMetricsCalculator
{
    public static IReadOnlyDictionary<int, CompetitionMetrics> Calculate(
        AppDbContext db,
        IReadOnlyCollection<Competition> competitions)
    {
        if (competitions.Count == 0)
            return new Dictionary<int, CompetitionMetrics>();

        var data = LoadData(
            db,
            competitions.Select(competition => competition.Id).ToArray());
        return CalculateMetrics(competitions, data);
    }

    public static async Task<IReadOnlyDictionary<int, CompetitionMetrics>> CalculateAsync(
        AppDbContext db,
        IReadOnlyCollection<Competition> competitions,
        CancellationToken cancellationToken = default)
    {
        if (competitions.Count == 0)
            return new Dictionary<int, CompetitionMetrics>();

        var data = await LoadDataAsync(
            db,
            competitions.Select(competition => competition.Id).ToArray(),
            cancellationToken);
        return CalculateMetrics(competitions, data);
    }

    private static MetricsData LoadData(
        AppDbContext db,
        int[] competitionIds)
    {
        var rounds = db.Rounds
            .AsNoTracking()
            .Where(round => competitionIds.Contains(round.CompetitionId))
            .Select(round => new RoundData(
                round.Id,
                round.CompetitionId,
                round.PlayerId))
            .ToList();
        var roundIds = rounds.Select(round => round.Id).ToArray();

        var individualScores = db.Scores
            .AsNoTracking()
            .Where(score => roundIds.Contains(score.RoundId) && score.Strokes > 0)
            .GroupBy(score => score.RoundId)
            .Select(group => new RoundProgressData(
                group.Key,
                group.Select(score => score.HoleNumber).Distinct().Count()))
            .ToList();

        var teams = db.Teams
            .AsNoTracking()
            .Where(team => competitionIds.Contains(team.CompetitionId))
            .Select(team => new TeamData(team.Id, team.CompetitionId))
            .ToList();
        var teamIds = teams.Select(team => team.Id).ToArray();

        var teamPlayers = db.TeamPlayers
            .AsNoTracking()
            .Where(teamPlayer => teamIds.Contains(teamPlayer.TeamId))
            .Select(teamPlayer => new TeamPlayerData(
                teamPlayer.TeamId,
                teamPlayer.PlayerId))
            .ToList();

        var teamRounds = db.TeamRounds
            .AsNoTracking()
            .Where(teamRound => competitionIds.Contains(teamRound.CompetitionId))
            .Select(teamRound => new TeamRoundData(
                teamRound.Id,
                teamRound.CompetitionId))
            .ToList();
        var teamRoundIds = teamRounds.Select(teamRound => teamRound.Id).ToArray();

        var teamScores = db.TeamScores
            .AsNoTracking()
            .Where(score => teamRoundIds.Contains(score.TeamRoundId) && score.Strokes > 0)
            .GroupBy(score => score.TeamRoundId)
            .Select(group => new RoundProgressData(
                group.Key,
                group.Select(score => score.HoleNumber).Distinct().Count()))
            .ToList();

        var matchPlayRounds = db.MatchPlayRounds
            .AsNoTracking()
            .Where(match => competitionIds.Contains(match.CompetitionId))
            .Select(match => new MatchPlayRoundData(
                match.Id,
                match.CompetitionId,
                match.IsFinished,
                match.CurrentHole,
                match.WinnerTeamId,
                match.StatusText,
                match.ResultText))
            .ToList();
        var matchPlayRoundIds = matchPlayRounds.Select(match => match.Id).ToArray();

        var matchPlayHoleResults = db.MatchPlayHoleResults
            .AsNoTracking()
            .Where(result => matchPlayRoundIds.Contains(result.MatchPlayRoundId))
            .Select(result => result.MatchPlayRoundId)
            .Distinct()
            .ToList();

        return new MetricsData(
            rounds,
            individualScores,
            teams,
            teamPlayers,
            teamRounds,
            teamScores,
            matchPlayRounds,
            matchPlayHoleResults);
    }

    private static async Task<MetricsData> LoadDataAsync(
        AppDbContext db,
        int[] competitionIds,
        CancellationToken cancellationToken)
    {
        var rounds = await db.Rounds
            .AsNoTracking()
            .Where(round => competitionIds.Contains(round.CompetitionId))
            .Select(round => new RoundData(
                round.Id,
                round.CompetitionId,
                round.PlayerId))
            .ToListAsync(cancellationToken);
        var roundIds = rounds.Select(round => round.Id).ToArray();

        var individualScores = await db.Scores
            .AsNoTracking()
            .Where(score => roundIds.Contains(score.RoundId) && score.Strokes > 0)
            .GroupBy(score => score.RoundId)
            .Select(group => new RoundProgressData(
                group.Key,
                group.Select(score => score.HoleNumber).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var teams = await db.Teams
            .AsNoTracking()
            .Where(team => competitionIds.Contains(team.CompetitionId))
            .Select(team => new TeamData(team.Id, team.CompetitionId))
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(team => team.Id).ToArray();

        var teamPlayers = await db.TeamPlayers
            .AsNoTracking()
            .Where(teamPlayer => teamIds.Contains(teamPlayer.TeamId))
            .Select(teamPlayer => new TeamPlayerData(
                teamPlayer.TeamId,
                teamPlayer.PlayerId))
            .ToListAsync(cancellationToken);

        var teamRounds = await db.TeamRounds
            .AsNoTracking()
            .Where(teamRound => competitionIds.Contains(teamRound.CompetitionId))
            .Select(teamRound => new TeamRoundData(
                teamRound.Id,
                teamRound.CompetitionId))
            .ToListAsync(cancellationToken);
        var teamRoundIds = teamRounds.Select(teamRound => teamRound.Id).ToArray();

        var teamScores = await db.TeamScores
            .AsNoTracking()
            .Where(score => teamRoundIds.Contains(score.TeamRoundId) && score.Strokes > 0)
            .GroupBy(score => score.TeamRoundId)
            .Select(group => new RoundProgressData(
                group.Key,
                group.Select(score => score.HoleNumber).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var matchPlayRounds = await db.MatchPlayRounds
            .AsNoTracking()
            .Where(match => competitionIds.Contains(match.CompetitionId))
            .Select(match => new MatchPlayRoundData(
                match.Id,
                match.CompetitionId,
                match.IsFinished,
                match.CurrentHole,
                match.WinnerTeamId,
                match.StatusText,
                match.ResultText))
            .ToListAsync(cancellationToken);
        var matchPlayRoundIds = matchPlayRounds.Select(match => match.Id).ToArray();

        var matchPlayHoleResults = await db.MatchPlayHoleResults
            .AsNoTracking()
            .Where(result => matchPlayRoundIds.Contains(result.MatchPlayRoundId))
            .Select(result => result.MatchPlayRoundId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new MetricsData(
            rounds,
            individualScores,
            teams,
            teamPlayers,
            teamRounds,
            teamScores,
            matchPlayRounds,
            matchPlayHoleResults);
    }

    private static IReadOnlyDictionary<int, CompetitionMetrics> CalculateMetrics(
        IReadOnlyCollection<Competition> competitions,
        MetricsData data)
    {
        var teamCompetitionById = data.Teams.ToDictionary(
            team => team.Id,
            team => team.CompetitionId);
        var participantsByCompetition = data.Rounds
            .GroupBy(round => round.CompetitionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(round => round.PlayerId).ToHashSet());

        foreach (var teamPlayer in data.TeamPlayers)
        {
            if (!teamCompetitionById.TryGetValue(
                    teamPlayer.TeamId,
                    out var competitionId))
            {
                continue;
            }

            if (!participantsByCompetition.TryGetValue(
                    competitionId,
                    out var participants))
            {
                participants = new HashSet<int>();
                participantsByCompetition[competitionId] = participants;
            }

            participants.Add(teamPlayer.PlayerId);
        }

        var individualHolesByRound = data.IndividualScores
            .ToDictionary(
                progress => progress.RoundId,
                progress => progress.HolesPlayed);
        var teamHolesByRound = data.TeamScores
            .ToDictionary(
                progress => progress.RoundId,
                progress => progress.HolesPlayed);
        var matchesWithHoleResults = data.MatchPlayHoleResults.ToHashSet();

        var metrics = new Dictionary<int, CompetitionMetrics>();

        foreach (var competition in competitions)
        {
            var participantsCount = participantsByCompetition.TryGetValue(
                competition.Id,
                out var participants)
                ? participants.Count
                : 0;

            int totalRounds;
            int completedRounds;
            int startedRounds;

            if (IsMatchPlay(competition.CompetitionType))
            {
                var matchRounds = data.MatchPlayRounds
                    .Where(match => match.CompetitionId == competition.Id)
                    .ToList();

                totalRounds = matchRounds.Count;
                completedRounds = matchRounds.Count(match => match.IsFinished);
                startedRounds = matchRounds.Count(match =>
                    match.IsFinished
                    || match.CurrentHole > 1
                    || match.WinnerTeamId.HasValue
                    || !string.Equals(match.StatusText, "AS", StringComparison.Ordinal)
                    || !string.IsNullOrWhiteSpace(match.ResultText)
                    || matchesWithHoleResults.Contains(match.Id));
            }
            else if (IsDoubles(competition.CompetitionType))
            {
                var competitionTeamRounds = data.TeamRounds
                    .Where(teamRound => teamRound.CompetitionId == competition.Id)
                    .ToList();

                totalRounds = competitionTeamRounds.Count;
                completedRounds = competitionTeamRounds.Count(teamRound =>
                    teamHolesByRound.GetValueOrDefault(teamRound.Id) >= 18);
                startedRounds = competitionTeamRounds.Count(teamRound =>
                    teamHolesByRound.GetValueOrDefault(teamRound.Id) > 0);
            }
            else
            {
                var competitionRounds = data.Rounds
                    .Where(round => round.CompetitionId == competition.Id)
                    .ToList();

                totalRounds = competitionRounds.Count;
                completedRounds = competitionRounds.Count(round =>
                    individualHolesByRound.GetValueOrDefault(round.Id) >= 18);
                startedRounds = competitionRounds.Count(round =>
                    individualHolesByRound.GetValueOrDefault(round.Id) > 0);
            }

            metrics[competition.Id] = new CompetitionMetrics(
                competition.Id,
                participantsCount,
                totalRounds,
                completedRounds,
                startedRounds,
                competition.Status != CompetitionStatus.Draft,
                competition.Status == CompetitionStatus.Finished);
        }

        return metrics;
    }

    private static bool IsDoubles(CompetitionType competitionType) =>
        competitionType is CompetitionType.DoublesScramble
            or CompetitionType.DoublesFourball
            or CompetitionType.DoublesFoursome;

    private static bool IsMatchPlay(CompetitionType competitionType) =>
        competitionType is CompetitionType.MatchPlayIndividual
            or CompetitionType.MatchPlayFourball
            or CompetitionType.MatchPlayFoursome
            or CompetitionType.MatchPlayScramble;

    private sealed record RoundData(int Id, int CompetitionId, int PlayerId);
    private sealed record RoundProgressData(int RoundId, int HolesPlayed);
    private sealed record TeamData(int Id, int CompetitionId);
    private sealed record TeamPlayerData(int TeamId, int PlayerId);
    private sealed record TeamRoundData(int Id, int CompetitionId);
    private sealed record MatchPlayRoundData(
        int Id,
        int CompetitionId,
        bool IsFinished,
        int CurrentHole,
        int? WinnerTeamId,
        string StatusText,
        string ResultText);
    private sealed record MetricsData(
        List<RoundData> Rounds,
        List<RoundProgressData> IndividualScores,
        List<TeamData> Teams,
        List<TeamPlayerData> TeamPlayers,
        List<TeamRoundData> TeamRounds,
        List<RoundProgressData> TeamScores,
        List<MatchPlayRoundData> MatchPlayRounds,
        List<int> MatchPlayHoleResults);
}
