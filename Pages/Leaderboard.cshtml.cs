using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class LeaderboardModel : PageModel
    {
        protected readonly AppDbContext _db;

        public LeaderboardModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        public Competition? Competition { get; set; }

        public bool IsTraining =>
            string.Equals(Competition?.Mode, "Training", StringComparison.OrdinalIgnoreCase);

        public bool IsCompetition => !IsTraining;

        public bool UserCanManageCompetition { get; set; }
        public bool ShowResultsButton => Competition?.Status == CompetitionStatus.Finished;
        public bool ShowLiveButtons => Competition?.Status == CompetitionStatus.InProgress;

        public List<LeaderboardRow> Rows { get; set; } = new();

        public int CourseParTotal { get; set; }
        public int LeaderPlayedPar { get; set; }
        public LeaderboardRow? LeaderRow { get; set; }

        public Dictionary<int, SquadProgress> SquadProgressById { get; set; } = new();

        public bool IsDoublesCompetition =>
            Competition != null &&
            (Competition.CompetitionType == CompetitionType.DoublesScramble
             || Competition.CompetitionType == CompetitionType.DoublesFourball
             || Competition.CompetitionType == CompetitionType.DoublesFoursome);

        public bool IsFourballCompetition =>
            Competition != null &&
            Competition.CompetitionType == CompetitionType.DoublesFourball;

        public bool IsMatchPlayCompetition =>
            Competition != null &&
            (Competition.CompetitionType == CompetitionType.MatchPlayIndividual
             || Competition.CompetitionType == CompetitionType.MatchPlayFourball
             || Competition.CompetitionType == CompetitionType.MatchPlayFoursome
             || Competition.CompetitionType == CompetitionType.MatchPlayScramble);

        public bool IsMatchPlayIndividual =>
            Competition?.CompetitionType == CompetitionType.MatchPlayIndividual;

        public class SquadProgress
        {
            public int SquadId { get; set; }
            public string SquadName { get; set; } = "";
            public int StartHole { get; set; }
            public int MinHolesPlayed { get; set; }
            public bool Finished => MinHolesPlayed >= 18;
            public int? CurrentHole { get; set; }
        }

        public class LeaderboardRow
        {
            public int RoundId { get; set; }
            public string PlayerName { get; set; } = "";

            public int HolesPlayed { get; set; }
            public int TotalStrokes { get; set; }

            public int TotalPar { get; set; }
            public int DiffToPar { get; set; }

            public int Difference { get; set; }
            public double Average { get; set; }

            public int? SquadId { get; set; }
            public string? SquadName { get; set; }
            public int? SquadStartHole { get; set; }
        }

        public class MatchPlayRow
        {
            public int MatchId { get; set; }
            public int SquadId { get; set; }
            public string TeamAName { get; set; } = "";
            public string TeamBName { get; set; } = "";
            public int CurrentHole { get; set; }
            public string StatusText { get; set; } = "AS";
            public string ResultText { get; set; } = "";
            public bool IsFinished { get; set; }
        }

        public List<MatchPlayRow> MatchPlayRows { get; set; } = new();

        public IActionResult OnGet()
        {
            Competition = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            var accessResult = GetCompetitionAccessResult(Competition);
            if (accessResult != null)
                return accessResult;

            UserCanManageCompetition = CanManageCompetition(Competition);

            if (IsMatchPlayCompetition)
            {
                return BuildMatchPlayLeaderboard();
            }

            if (IsDoublesCompetition)
            {
                return BuildTeamsLeaderboard();
            }

            if (Competition.CourseId == null)
            {
                TempData["Error"] = "Aucun parcours n'est associé à cette compétition. Le Live est désactivé.";
                return RedirectToPage("/Competitions");
            }

            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            CourseParTotal = parByHole.Count > 0 ? parByHole.Values.Sum() : 0;

            var rounds = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Squad)
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToList();

            var holesPlayedByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            var scoresByRound = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(g => g.Key, g => g.ToList());

            BuildSquadProgress(rounds, holesPlayedByRoundId);

            Rows = new List<LeaderboardRow>();

            foreach (var r in rounds)
            {
                scoresByRound.TryGetValue(r.Id, out var playerScores);
                playerScores ??= new List<Score>();

                int holesPlayed = holesPlayedByRoundId.TryGetValue(r.Id, out int hp) ? hp : 0;

                int totalStrokes = playerScores.Sum(s => s.Strokes);

                int totalPar = 0;
                foreach (var sc in playerScores)
                {
                    if (parByHole.TryGetValue(sc.HoleNumber, out int par))
                        totalPar += par;
                }

                int diffToPar = (holesPlayed == 0) ? 0 : (totalStrokes - totalPar);
                double average = holesPlayed > 0 ? (double)totalStrokes / holesPlayed : 0;

                Rows.Add(new LeaderboardRow
                {
                    RoundId = r.Id,
                    PlayerName = r.Player != null
                        ? (r.Player.FirstName + " " + r.Player.LastName).Trim()
                        : ("PlayerId=" + r.PlayerId),

                    HolesPlayed = holesPlayed,
                    TotalStrokes = totalStrokes,
                    TotalPar = totalPar,
                    DiffToPar = diffToPar,
                    Average = average,

                    SquadId = r.SquadId,
                    SquadName = r.Squad?.Name,
                    SquadStartHole = r.Squad?.StartHole
                });
            }

            Rows = Rows
                .OrderBy(x => x.HolesPlayed == 0 ? int.MaxValue : x.DiffToPar)
                .ThenByDescending(x => x.HolesPlayed)
                .ThenBy(x => x.TotalStrokes)
                .ThenBy(x => x.PlayerName)
                .ToList();

            var leader = Rows.FirstOrDefault(r => r.HolesPlayed > 0);
            LeaderRow = leader;
            LeaderPlayedPar = leader?.TotalPar ?? 0;

            foreach (var row in Rows)
            {
                row.Difference = (leader != null && row.HolesPlayed > 0)
                    ? row.DiffToPar - leader.DiffToPar
                    : 0;
            }

            return Page();
        }

        private IActionResult? GetCompetitionAccessResult(Competition competition)
        {
            if (CanAccessCompetition(competition))
                return null;

            if (User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login");

            return Forbid();
        }

        private bool CanAccessCompetition(Competition competition)
        {
            if (competition.Visibility == CompetitionVisibility.Public)
                return true;

            if (User.Identity?.IsAuthenticated != true)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            var currentUser = _db.AppUsers
                .AsNoTracking()
                .FirstOrDefault(u => u.Email == email);

            if (currentUser == null)
                return false;

            if (string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
            {
                if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                    return true;

                if (competition.CreatedByUserId.HasValue && currentUser.Id == competition.CreatedByUserId.Value)
                    return true;
            }

            if (string.Equals(role, "Player", StringComparison.OrdinalIgnoreCase))
            {
                if (currentUser.PlayerId.HasValue && IsPlayerParticipant(competition.Id, currentUser.PlayerId.Value))
                    return true;
            }

            if (competition.Visibility == CompetitionVisibility.Private)
            {
                if (competition.CreatedByUserId.HasValue && currentUser.Id == competition.CreatedByUserId.Value)
                    return true;
            }

            if (competition.Visibility == CompetitionVisibility.Club)
            {
                if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                    return true;

                if (currentUser.PlayerId.HasValue && IsPlayerParticipant(competition.Id, currentUser.PlayerId.Value))
                    return true;
            }

            return false;
        }

        private bool CanManageCompetition(Competition competition)
        {
            if (User.Identity?.IsAuthenticated != true)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            var currentUser = _db.AppUsers
                .AsNoTracking()
                .FirstOrDefault(u => u.Email == email);

            if (currentUser == null)
                return false;

            if (string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
            {
                if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                    return true;

                if (competition.CreatedByUserId.HasValue && currentUser.Id == competition.CreatedByUserId.Value)
                    return true;
            }

            return false;
        }

        private bool IsPlayerParticipant(int competitionId, int playerId)
        {
            bool inIndividualRounds = _db.Rounds
                .AsNoTracking()
                .Any(r => r.CompetitionId == competitionId && r.PlayerId == playerId);

            if (inIndividualRounds)
                return true;

            bool inTeams = _db.TeamRounds
                .AsNoTracking()
                .Include(tr => tr.Team)
                .ThenInclude(t => t.TeamPlayers)
                .Any(tr =>
                    tr.CompetitionId == competitionId &&
                    tr.Team != null &&
                    tr.Team.TeamPlayers.Any(tp => tp.PlayerId == playerId));

            if (inTeams)
                return true;

            bool inMatchPlay = _db.MatchPlayRounds
                .AsNoTracking()
                .Include(m => m.TeamA)
                .ThenInclude(t => t.TeamPlayers)
                .Include(m => m.TeamB)
                .ThenInclude(t => t.TeamPlayers)
                .Any(m =>
                    m.CompetitionId == competitionId &&
                    (
                        (m.TeamA != null && m.TeamA.TeamPlayers.Any(tp => tp.PlayerId == playerId)) ||
                        (m.TeamB != null && m.TeamB.TeamPlayers.Any(tp => tp.PlayerId == playerId))
                    ));

            return inMatchPlay;
        }

        private IActionResult BuildMatchPlayLeaderboard()
        {
            if (Competition == null)
                return RedirectToPage("/Competitions");

            var squads = _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == CompetitionId)
                .OrderBy(s => s.Id)
                .ToList();

            SquadProgressById = squads.ToDictionary(
                s => s.Id,
                s => new SquadProgress
                {
                    SquadId = s.Id,
                    SquadName = s.Name,
                    StartHole = s.StartHole,
                    MinHolesPlayed = 0,
                    CurrentHole = s.StartHole
                });

            var matches = _db.MatchPlayRounds
                .Include(m => m.TeamA)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .Include(m => m.TeamB)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .AsNoTracking()
                .Where(m => m.CompetitionId == CompetitionId)
                .OrderBy(m => m.SquadId)
                .ThenBy(m => m.Id)
                .ToList();

            MatchPlayRows = matches.Select(m => new MatchPlayRow
            {
                MatchId = m.Id,
                SquadId = m.SquadId,
                TeamAName = string.Join(" / ",
                    m.TeamA?.TeamPlayers?
                        .OrderBy(tp => tp.Order)
                        .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}")
                    ?? new List<string>()),
                TeamBName = string.Join(" / ",
                    m.TeamB?.TeamPlayers?
                        .OrderBy(tp => tp.Order)
                        .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}")
                    ?? new List<string>()),
                CurrentHole = m.CurrentHole,
                StatusText = m.StatusText,
                ResultText = m.ResultText,
                IsFinished = m.IsFinished
            }).ToList();

            Rows = new List<LeaderboardRow>();
            CourseParTotal = 0;
            LeaderPlayedPar = 0;
            LeaderRow = null;

            return Page();
        }

        private void BuildSquadProgress(List<Round> rounds, Dictionary<int, int> holesPlayedByRoundId)
        {
            SquadProgressById = new Dictionary<int, SquadProgress>();

            var bySquad = rounds
                .Where(r => r.SquadId.HasValue && r.Squad != null)
                .GroupBy(r => r.SquadId!.Value);

            foreach (var g in bySquad)
            {
                int squadId = g.Key;
                var firstRound = g.First();
                int startHole = firstRound.Squad!.StartHole;
                string squadName = firstRound.Squad!.Name;

                int minHoles = int.MaxValue;

                foreach (var r in g)
                {
                    int hp = holesPlayedByRoundId.TryGetValue(r.Id, out int v) ? v : 0;
                    if (hp < minHoles) minHoles = hp;
                }

                if (minHoles == int.MaxValue) minHoles = 0;

                int? currentHole = null;
                if (minHoles < 18)
                    currentHole = NextHole(startHole, minHoles);

                SquadProgressById[squadId] = new SquadProgress
                {
                    SquadId = squadId,
                    SquadName = squadName,
                    StartHole = startHole,
                    MinHolesPlayed = minHoles,
                    CurrentHole = currentHole
                };
            }
        }

        private int NextHole(int startHole, int offset)
        {
            return ((startHole - 1 + offset) % 18) + 1;
        }

        public string FormatDiffToPar(int diffToPar, bool hasScore)
        {
            if (!hasScore) return "-";
            if (diffToPar == 0) return "E";
            if (diffToPar > 0) return $"+{diffToPar}";
            return diffToPar.ToString();
        }

        public string FormatDifference(int difference, bool hasScore)
        {
            if (!hasScore) return "-";
            if (difference == 0) return "E";
            if (difference > 0) return $"+{difference}";
            return difference.ToString();
        }

        public string GetParCssClass(int diffToPar)
        {
            if (diffToPar < 0) return "par-under";
            if (diffToPar > 0) return "par-over";
            return "par-even";
        }

        public string GetDifferenceCssClass(int difference, bool hasScore)
        {
            if (!hasScore) return "";
            if (difference == 0) return "par-even";
            if (difference > 0) return "par-over";
            return "par-under";
        }

        public string GetSquadScoreUrl(int squadId)
        {
            if (Competition == null)
                return "#";

            if (!SquadProgressById.TryGetValue(squadId, out var progress))
                return "#";

            int hole = progress.CurrentHole ?? 18;

            if (IsDoublesCompetition)
                return $"/Teams/Score?competitionId={CompetitionId}&squadId={squadId}&hole={hole}";

            return $"/Squads/Score?competitionId={CompetitionId}&squadId={squadId}&hole={hole}";
        }

        public string GetMatchPlayScoreUrl(int matchId)
        {
            return $"/MatchPlayScore?matchId={matchId}";
        }

        private IActionResult BuildTeamsLeaderboard()
        {
            if (Competition == null || Competition.CourseId == null)
                return RedirectToPage("/Competitions");

            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            CourseParTotal = parByHole.Count > 0 ? parByHole.Values.Sum() : 0;

            var teamRounds = _db.TeamRounds
                .Include(tr => tr.Team)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .Include(tr => tr.Squad)
                .AsNoTracking()
                .Where(tr => tr.CompetitionId == CompetitionId)
                .ToList();

            var teamRoundIds = teamRounds.Select(tr => tr.Id).ToList();

            var teamScores = _db.TeamScores
                .AsNoTracking()
                .Where(s => teamRoundIds.Contains(s.TeamRoundId) && s.Strokes > 0)
                .ToList();

            var holesPlayedByTeamRoundId = teamScores
                .GroupBy(s => s.TeamRoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            var scoresByTeamRound = teamScores
                .GroupBy(s => s.TeamRoundId)
                .ToDictionary(g => g.Key, g => g.ToList());

            BuildTeamSquadProgress(teamRounds, holesPlayedByTeamRoundId);

            Rows = new List<LeaderboardRow>();

            foreach (var tr in teamRounds)
            {
                scoresByTeamRound.TryGetValue(tr.Id, out var scoresForTeam);
                scoresForTeam ??= new List<TeamScore>();

                int holesPlayed = holesPlayedByTeamRoundId.TryGetValue(tr.Id, out int hp) ? hp : 0;
                int totalStrokes = scoresForTeam.Sum(s => s.Strokes);

                int totalPar = 0;
                foreach (var sc in scoresForTeam)
                {
                    if (parByHole.TryGetValue(sc.HoleNumber, out int par))
                        totalPar += par;
                }

                int diffToPar = holesPlayed == 0 ? 0 : totalStrokes - totalPar;
                double average = holesPlayed > 0 ? (double)totalStrokes / holesPlayed : 0;

                string teamDisplayName;
                if (tr.Team != null && tr.Team.TeamPlayers.Any())
                {
                    teamDisplayName = string.Join(" / ",
                        tr.Team.TeamPlayers
                            .OrderBy(tp => tp.Order)
                            .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}"));
                }
                else if (tr.Team != null && !string.IsNullOrWhiteSpace(tr.Team.Name))
                {
                    teamDisplayName = tr.Team.Name;
                }
                else
                {
                    teamDisplayName = $"Équipe {tr.TeamId}";
                }

                Rows.Add(new LeaderboardRow
                {
                    RoundId = tr.Id,
                    PlayerName = teamDisplayName,

                    HolesPlayed = holesPlayed,
                    TotalStrokes = totalStrokes,

                    TotalPar = totalPar,
                    DiffToPar = diffToPar,
                    Average = average,

                    SquadId = tr.SquadId,
                    SquadName = tr.Squad?.Name,
                    SquadStartHole = tr.Squad?.StartHole
                });
            }

            Rows = Rows
                .OrderBy(x => x.HolesPlayed == 0 ? int.MaxValue : x.DiffToPar)
                .ThenByDescending(x => x.HolesPlayed)
                .ThenBy(x => x.TotalStrokes)
                .ThenBy(x => x.PlayerName)
                .ToList();

            var leader = Rows.FirstOrDefault(r => r.HolesPlayed > 0);
            LeaderRow = leader;
            LeaderPlayedPar = leader?.TotalPar ?? 0;

            foreach (var row in Rows)
            {
                row.Difference = (leader != null && row.HolesPlayed > 0)
                    ? row.DiffToPar - leader.DiffToPar
                    : 0;
            }

            return Page();
        }

        private void BuildTeamSquadProgress(List<TeamRound> teamRounds, Dictionary<int, int> holesPlayedByTeamRoundId)
        {
            SquadProgressById = new Dictionary<int, SquadProgress>();

            var bySquad = teamRounds
                .Where(tr => tr.SquadId.HasValue && tr.Squad != null)
                .GroupBy(tr => tr.SquadId!.Value);

            foreach (var g in bySquad)
            {
                int squadId = g.Key;
                var firstTeamRound = g.First();
                int startHole = firstTeamRound.Squad!.StartHole;
                string squadName = firstTeamRound.Squad!.Name;

                int minHoles = int.MaxValue;

                foreach (var tr in g)
                {
                    int hp = holesPlayedByTeamRoundId.TryGetValue(tr.Id, out int v) ? v : 0;
                    if (hp < minHoles)
                        minHoles = hp;
                }

                if (minHoles == int.MaxValue)
                    minHoles = 0;

                int? currentHole = null;
                if (minHoles < 18)
                    currentHole = NextHole(startHole, minHoles);

                SquadProgressById[squadId] = new SquadProgress
                {
                    SquadId = squadId,
                    SquadName = squadName,
                    StartHole = startHole,
                    MinHolesPlayed = minHoles,
                    CurrentHole = currentHole
                };
            }
        }
    }
}