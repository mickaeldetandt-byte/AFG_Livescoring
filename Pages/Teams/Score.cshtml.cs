using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Teams
{
    [Authorize(Roles = "Admin,Organizer")]
    public class ScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public ScoreModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SquadId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Hole { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool Edit { get; set; }

        [BindProperty]
        public int CurrentHole { get; set; }

        public Competition? Competition { get; set; }
        public Squad? Squad { get; set; }

        public int? CurrentPar { get; set; }
        public bool IsHoleValidated { get; set; }
        public bool IsLastHole { get; set; }
        public bool IsSquadLocked { get; set; }
        public bool IsCompetitionFinished { get; set; }

        public bool IsFourballCompetition =>
            Competition != null &&
            Competition.CompetitionType == CompetitionType.DoublesFourball;

        public bool IsScrambleOrFoursomeCompetition =>
            Competition != null &&
            (Competition.CompetitionType == CompetitionType.DoublesScramble ||
             Competition.CompetitionType == CompetitionType.DoublesFoursome);

        public List<TeamScoreRow> Teams { get; set; } = new();

        public class TeamScoreRow
        {
            public int TeamRoundId { get; set; }
            public int TeamId { get; set; }
            public string TeamName { get; set; } = "";
            public string PlayersDisplay { get; set; } = "";

            public int? Player1Id { get; set; }
            public string Player1Name { get; set; } = "";

            public int? Player2Id { get; set; }
            public string Player2Name { get; set; } = "";

            public int Strokes { get; set; }

            public int Player1Strokes { get; set; }
            public int Player2Strokes { get; set; }
        }

        private bool IsDoublesCompetition(Competition competition)
        {
            return competition.CompetitionType == CompetitionType.DoublesScramble
                || competition.CompetitionType == CompetitionType.DoublesFourball
                || competition.CompetitionType == CompetitionType.DoublesFoursome;
        }

        private IActionResult? CheckCompetitionFinished()
        {
            var competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (competition == null)
                return RedirectToPage("/Competitions");

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Error"] = "La compétition est terminée. Le scoring est verrouillé.";
                return RedirectToPage("/Competitions/Details", new { id = CompetitionId });
            }

            return null;
        }

        private bool ComputeIsSquadLocked()
        {
            if (CompetitionId <= 0 || SquadId <= 0)
                return false;

            return _db.TeamRounds.AsNoTracking().Any(tr =>
                tr.CompetitionId == CompetitionId &&
                tr.SquadId == SquadId &&
                tr.IsLocked);
        }

        private bool IsSquadComplete()
        {
            var teamRoundIds = _db.TeamRounds
                .AsNoTracking()
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .Select(tr => tr.Id)
                .ToList();

            if (teamRoundIds.Count == 0)
                return false;

            var counts = _db.TeamScores
                .AsNoTracking()
                .Where(s => teamRoundIds.Contains(s.TeamRoundId) && s.Strokes > 0)
                .GroupBy(s => s.TeamRoundId)
                .Select(g => new { TeamRoundId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.TeamRoundId, x => x.Count);

            return teamRoundIds.All(id => counts.ContainsKey(id) && counts[id] == 18);
        }

        private void LockSquad()
        {
            var roundsToLock = _db.TeamRounds
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .ToList();

            foreach (var r in roundsToLock)
                r.IsLocked = true;

            _db.SaveChanges();
        }

        private void UnlockSquad()
        {
            var roundsToUnlock = _db.TeamRounds
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .ToList();

            foreach (var r in roundsToUnlock)
                r.IsLocked = false;

            _db.SaveChanges();
        }

        private int GetAutoCurrentHole(int competitionId, int squadId, int startHole)
        {
            var teamRounds = _db.TeamRounds
                .AsNoTracking()
                .Where(tr => tr.CompetitionId == competitionId && tr.SquadId == squadId)
                .Select(tr => tr.Id)
                .ToList();

            if (teamRounds.Count == 0)
                return startHole;

            var scores = _db.TeamScores
                .AsNoTracking()
                .Where(s => teamRounds.Contains(s.TeamRoundId))
                .Select(s => new { s.TeamRoundId, s.HoleNumber })
                .ToList();

            for (int holeNumber = 1; holeNumber <= 18; holeNumber++)
            {
                bool holeCompleteForSquad = teamRounds.All(teamRoundId =>
                    scores.Any(s => s.TeamRoundId == teamRoundId && s.HoleNumber == holeNumber));

                if (!holeCompleteForSquad)
                    return holeNumber;
            }

            return 18;
        }

        private Dictionary<int, int> ReadScoreDictionaryFromForm(string prefix)
        {
            var result = new Dictionary<int, int>();

            foreach (var key in Request.Form.Keys)
            {
                if (!key.StartsWith(prefix + "[") || !key.EndsWith("]"))
                    continue;

                var idText = key.Substring(prefix.Length + 1, key.Length - prefix.Length - 2);

                if (!int.TryParse(idText, out int id))
                    continue;

                var valueText = Request.Form[key].ToString();

                if (!int.TryParse(valueText, out int value))
                    continue;

                result[id] = value;
            }

            return result;
        }

        private bool HasAnyPostedKey(string prefix)
        {
            return Request.Form.Keys.Any(k => k.StartsWith(prefix + "[") && k.EndsWith("]"));
        }

        public IActionResult OnGet()
        {
            Competition = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition(Competition))
            {
                TempData["Error"] = "Cette page est réservée aux compétitions doubles.";
                return RedirectToPage("/Competitions");
            }

            if (Competition.CourseId == null)
            {
                TempData["Error"] = "Aucun parcours n'est associé à cette compétition.";
                return RedirectToPage("/Competitions");
            }

            IsCompetitionFinished = Competition.Status == CompetitionStatus.Finished;

            Squad = _db.Squads
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == SquadId);

            if (Squad == null || Squad.CompetitionId != CompetitionId)
            {
                TempData["Error"] = "Squad introuvable ou ne correspond pas à la compétition.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            IsSquadLocked = ComputeIsSquadLocked();

            if (Hole.HasValue)
                CurrentHole = Hole.Value;
            else
                CurrentHole = GetAutoCurrentHole(CompetitionId, SquadId, Squad.StartHole);

            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            IsLastHole = (CurrentHole == 18);

            CurrentPar = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value && h.HoleNumber == CurrentHole)
                .Select(h => (int?)h.Par)
                .FirstOrDefault();

            var teamRounds = _db.TeamRounds
                .Include(tr => tr.Team)
                    .ThenInclude(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                .AsNoTracking()
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .ToList();

            if (teamRounds.Count == 0)
            {
                TempData["Error"] = "Aucune équipe n'est affectée à ce squad.";
                return RedirectToPage("/Teams/Manage", new { competitionId = CompetitionId });
            }

            var teamRoundIds = teamRounds.Select(tr => tr.Id).ToList();

            var existingScores = _db.TeamScores
                .AsNoTracking()
                .Where(s => teamRoundIds.Contains(s.TeamRoundId) && s.HoleNumber == CurrentHole)
                .ToList();

            IsHoleValidated = existingScores.Any();

            var existingScoreByTeamRoundId = existingScores
                .GroupBy(s => s.TeamRoundId)
                .ToDictionary(g => g.Key, g => g.First());

            Teams = teamRounds
                .OrderBy(tr => tr.Team!.Name)
                .Select(tr =>
                {
                    var orderedPlayers = tr.Team?.TeamPlayers
                        .OrderBy(tp => tp.Order)
                        .ToList() ?? new List<TeamPlayer>();

                    var p1 = orderedPlayers.ElementAtOrDefault(0);
                    var p2 = orderedPlayers.ElementAtOrDefault(1);

                    existingScoreByTeamRoundId.TryGetValue(tr.Id, out var existingScore);

                    return new TeamScoreRow
                    {
                        TeamRoundId = tr.Id,
                        TeamId = tr.TeamId,
                        TeamName = tr.Team?.Name ?? $"Equipe {tr.TeamId}",
                        PlayersDisplay = tr.Team != null
                            ? string.Join(" / ",
                                tr.Team.TeamPlayers
                                    .OrderBy(tp => tp.Order)
                                    .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}"))
                            : "",
                        Player1Id = p1?.PlayerId,
                        Player1Name = p1 != null ? $"{p1.Player.FirstName} {p1.Player.LastName}" : "",
                        Player2Id = p2?.PlayerId,
                        Player2Name = p2 != null ? $"{p2.Player.FirstName} {p2.Player.LastName}" : "",
                        Strokes = existingScore?.Strokes ?? 0,
                        Player1Strokes = existingScore?.Player1Strokes ?? 0,
                        Player2Strokes = existingScore?.Player2Strokes ?? 0
                    };
                })
                .ToList();

            if (IsCompetitionFinished)
            {
                TempData["Info"] = "Compétition terminée : consultation uniquement.";
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            var finishedCheck = CheckCompetitionFinished();
            if (finishedCheck != null)
                return finishedCheck;

            IsSquadLocked = ComputeIsSquadLocked();
            if (IsSquadLocked)
            {
                TempData["Error"] = "Carte validée : modification impossible.";
                return RedirectToPage("/Teams/Score", new
                {
                    competitionId = CompetitionId,
                    squadId = SquadId,
                    hole = CurrentHole
                });
            }

            Competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            var squad = _db.Squads.AsNoTracking().FirstOrDefault(s => s.Id == SquadId);
            if (squad == null || squad.CompetitionId != CompetitionId)
            {
                TempData["Error"] = "Squad introuvable ou ne correspond pas à la compétition.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            var allowedTeamRoundIds = _db.TeamRounds.AsNoTracking()
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .Select(tr => tr.Id)
                .ToHashSet();

            if (IsFourballCompetition)
            {
                var player1Scores = ReadScoreDictionaryFromForm("Player1Scores");
                var player2Scores = ReadScoreDictionaryFromForm("Player2Scores");

                if (!HasAnyPostedKey("Player1Scores") || !HasAnyPostedKey("Player2Scores") ||
                    player1Scores.Count == 0 || player2Scores.Count == 0)
                {
                    TempData["Error"] = "Aucun score joueur reçu.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                var postedIds = player1Scores.Keys.Union(player2Scores.Keys).Distinct().ToList();

                if (postedIds.Any(id => !allowedTeamRoundIds.Contains(id)))
                {
                    TempData["Error"] = "Données invalides (TeamRound non autorisé).";
                    return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
                }

                if (postedIds.Any(id =>
                    !player1Scores.ContainsKey(id) ||
                    !player2Scores.ContainsKey(id) ||
                    player1Scores[id] <= 0 ||
                    player2Scores[id] <= 0))
                {
                    TempData["Error"] = "Les 2 joueurs de chaque équipe doivent avoir un score supérieur à 0.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                bool alreadyValidated = _db.TeamScores.AsNoTracking().Any(s =>
                    postedIds.Contains(s.TeamRoundId) &&
                    s.HoleNumber == CurrentHole);

                if (alreadyValidated)
                {
                    TempData["Error"] = "Ce trou est déjà validé. Utilise 'Corriger le trou' pour le réouvrir.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                foreach (var teamRoundId in postedIds)
                {
                    int p1 = player1Scores[teamRoundId];
                    int p2 = player2Scores[teamRoundId];
                    int teamScore = Math.Min(p1, p2);

                    _db.TeamScores.Add(new TeamScore
                    {
                        TeamRoundId = teamRoundId,
                        HoleNumber = CurrentHole,
                        Strokes = teamScore,
                        Player1Strokes = p1,
                        Player2Strokes = p2
                    });
                }

                _db.SaveChanges();
            }
            else
            {
                var scores = ReadScoreDictionaryFromForm("Scores");

                if (!HasAnyPostedKey("Scores") || scores.Count == 0)
                {
                    TempData["Error"] = "Aucun score reçu.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                if (scores.Values.Any(v => v <= 0))
                {
                    TempData["Error"] = "Toutes les équipes doivent avoir un score supérieur à 0.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                var postedTeamRoundIds = scores.Keys.ToList();
                if (postedTeamRoundIds.Any(id => !allowedTeamRoundIds.Contains(id)))
                {
                    TempData["Error"] = "Données invalides (TeamRound non autorisé).";
                    return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
                }

                bool alreadyValidated = _db.TeamScores.AsNoTracking().Any(s =>
                    postedTeamRoundIds.Contains(s.TeamRoundId) &&
                    s.HoleNumber == CurrentHole);

                if (alreadyValidated)
                {
                    TempData["Error"] = "Ce trou est déjà validé. Utilise 'Corriger le trou' pour le réouvrir.";
                    return RedirectToPage("/Teams/Score", new
                    {
                        competitionId = CompetitionId,
                        squadId = SquadId,
                        hole = CurrentHole,
                        edit = Edit
                    });
                }

                foreach (var kvp in scores)
                {
                    _db.TeamScores.Add(new TeamScore
                    {
                        TeamRoundId = kvp.Key,
                        HoleNumber = CurrentHole,
                        Strokes = kvp.Value,
                        Player1Strokes = null,
                        Player2Strokes = null
                    });
                }

                _db.SaveChanges();
            }

            if (CurrentHole >= 18)
            {
                return RedirectToPage("/Teams/Score", new
                {
                    competitionId = CompetitionId,
                    squadId = SquadId,
                    hole = CurrentHole,
                    edit = Edit
                });
            }

            return RedirectToPage("/Teams/Score", new
            {
                competitionId = CompetitionId,
                squadId = SquadId,
                hole = CurrentHole + 1,
                edit = Edit
            });
        }

        public IActionResult OnPostCorrect()
        {
            var finishedCheck = CheckCompetitionFinished();
            if (finishedCheck != null)
                return finishedCheck;

            IsSquadLocked = ComputeIsSquadLocked();
            if (IsSquadLocked)
            {
                TempData["Error"] = "Carte validée : modification impossible.";
                return RedirectToPage("/Teams/Score", new
                {
                    competitionId = CompetitionId,
                    squadId = SquadId,
                    hole = CurrentHole,
                    edit = Edit
                });
            }

            if (CurrentHole < 1 || CurrentHole > 18)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            var teamRoundIds = _db.TeamRounds.AsNoTracking()
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .Select(tr => tr.Id)
                .ToList();

            if (teamRoundIds.Count == 0)
            {
                TempData["Error"] = "Aucune équipe n'est affectée à ce squad.";
                return RedirectToPage("/Teams/Manage", new { competitionId = CompetitionId });
            }

            var toDelete = _db.TeamScores
                .Where(s => teamRoundIds.Contains(s.TeamRoundId) && s.HoleNumber == CurrentHole)
                .ToList();

            if (toDelete.Count == 0)
            {
                TempData["Error"] = "Aucun score à corriger sur ce trou.";
                return RedirectToPage("/Teams/Score", new
                {
                    competitionId = CompetitionId,
                    squadId = SquadId,
                    hole = CurrentHole,
                    edit = Edit
                });
            }

            _db.TeamScores.RemoveRange(toDelete);
            _db.SaveChanges();

            TempData["Info"] = $"Trou {CurrentHole} réouvert (scores supprimés).";
            return RedirectToPage("/Teams/Score", new
            {
                competitionId = CompetitionId,
                squadId = SquadId,
                hole = CurrentHole,
                edit = true
            });
        }

        public IActionResult OnPostLockCard()
        {
            var finishedCheck = CheckCompetitionFinished();
            if (finishedCheck != null)
                return finishedCheck;

            var squad = _db.Squads.AsNoTracking().FirstOrDefault(s => s.Id == SquadId);
            if (squad == null || squad.CompetitionId != CompetitionId)
            {
                TempData["Error"] = "Squad introuvable ou ne correspond pas à la compétition.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            if (!IsSquadComplete())
            {
                TempData["Error"] = "Impossible de valider la carte : tous les trous doivent être remplis pour toutes les équipes.";
                return RedirectToPage("/Teams/Score", new
                {
                    competitionId = CompetitionId,
                    squadId = SquadId,
                    hole = CurrentHole,
                    edit = Edit
                });
            }

            LockSquad();

            TempData["Success"] = "Carte validée. Modification impossible.";
            return RedirectToPage("/Teams/Score", new
            {
                competitionId = CompetitionId,
                squadId = SquadId,
                hole = CurrentHole,
                edit = false
            });
        }

        public IActionResult OnPostUnlock(int competitionId, int squadId, int hole)
        {
            CompetitionId = competitionId;
            SquadId = squadId;
            CurrentHole = hole;

            var finishedCheck = CheckCompetitionFinished();
            if (finishedCheck != null)
                return finishedCheck;

            var teamRounds = _db.TeamRounds
                .Where(tr => tr.CompetitionId == CompetitionId && tr.SquadId == SquadId)
                .ToList();

            if (teamRounds.Count == 0)
            {
                TempData["Error"] = "Aucun TeamRound trouvé pour ce squad.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            UnlockSquad();

            TempData["Info"] = "Squad déverrouillé (mode correction).";
            return RedirectToPage("/Teams/Score", new
            {
                competitionId = CompetitionId,
                squadId = SquadId,
                hole = CurrentHole,
                edit = true
            });
        }

        public IActionResult OnGetUnlock()
        {
            TempData["Error"] = "Déverrouillage uniquement en POST.";
            return RedirectToPage("/Teams/Score", new
            {
                competitionId = CompetitionId,
                squadId = SquadId,
                hole = Hole
            });
        }
    }
}