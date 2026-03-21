using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages.Squads
{
    [Authorize(Roles = "Admin,Club")]
    public class ManageModel : PageModel
    {
        private readonly AppDbContext _db;

        public ManageModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int competitionId { get; set; }

        public int CompetitionId => competitionId;

        public string CompetitionName { get; set; } = "";

        public bool HasScores { get; set; }
        public bool IsTraining { get; set; }
        public bool CanEditSquads { get; set; }
        public bool IsMatchPlayIndividual { get; set; }
        public bool IsMatchPlayCompetition { get; set; }
        public bool IsMatchPlayDoubles { get; set; }
        public string LockMessage { get; set; } = "";

        public int MinSquadSize { get; set; }
        public int MaxSquadSize { get; set; }

        public List<SquadView> Squads { get; set; } = new();
        public List<Round> UnassignedRounds { get; set; } = new();

        public class TeamView
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int? Player1Id { get; set; }
            public int? Player2Id { get; set; }
            public string DisplayName { get; set; } = "";
        }

        public class SquadView
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int StartHole { get; set; }
            public List<Round> Rounds { get; set; } = new();
            public List<MatchPlayRound> MatchPlayRounds { get; set; } = new();
            public List<TeamView> Teams { get; set; } = new();
        }

        public IActionResult OnGet()
        {
            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            CompetitionName = comp.Name;
            IsTraining = IsTrainingMode(comp);
            IsMatchPlayIndividual = comp.CompetitionType == CompetitionType.MatchPlayIndividual;
            IsMatchPlayCompetition = IsMatchPlayType(comp.CompetitionType);
            IsMatchPlayDoubles = IsDoublesMatchPlayType(comp.CompetitionType);

            var (min, max) = SquadRules.GetLimits(comp);
            MinSquadSize = min;
            MaxSquadSize = max;

            HasScores = CompetitionHasScores(competitionId);

            CanEditSquads = IsTraining || !HasScores;
            LockMessage = (!IsTraining && HasScores)
                ? "Squads verrouillés : la compétition a déjà démarré."
                : "";

            LoadSquadsAndUnassigned();

            return Page();
        }

        public IActionResult OnPostGenerate(int competitionId, int squadSize)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            CompetitionName = comp.Name;
            IsTraining = IsTrainingMode(comp);
            IsMatchPlayIndividual = comp.CompetitionType == CompetitionType.MatchPlayIndividual;
            IsMatchPlayCompetition = IsMatchPlayType(comp.CompetitionType);
            IsMatchPlayDoubles = IsDoublesMatchPlayType(comp.CompetitionType);

            var (minAllowed, maxAllowed) = SquadRules.GetLimits(comp);
            MinSquadSize = minAllowed;
            MaxSquadSize = maxAllowed;

            HasScores = CompetitionHasScores(competitionId);
            CanEditSquads = IsTraining || !HasScores;
            LockMessage = (!IsTraining && HasScores)
                ? "Squads verrouillés : la compétition a déjà démarré."
                : "";

            if (!CanEditSquads)
            {
                TempData["Message"] = LockMessage;
                return RedirectToPage(new { competitionId });
            }

            if (squadSize < minAllowed) squadSize = minAllowed;
            if (squadSize > maxAllowed) squadSize = maxAllowed;

            if (IsTraining)
            {
                foreach (var r in _db.Rounds.Where(r => r.CompetitionId == competitionId))
                    r.SquadId = null;

                var existingSquads = _db.Squads.Where(s => s.CompetitionId == competitionId).ToList();
                if (existingSquads.Any())
                    _db.Squads.RemoveRange(existingSquads);

                _db.SaveChanges();
            }

            var rounds = _db.Rounds
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == competitionId)
                .ToList();

            if (!rounds.Any())
            {
                TempData["Message"] = "Aucun participant dans cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (!IsTraining && _db.Squads.Any(s => s.CompetitionId == competitionId))
            {
                TempData["Message"] = "Des squads existent déjà. Clique sur Réinitialiser avant de regénérer.";
                return RedirectToPage(new { competitionId });
            }

            var rnd = new Random();
            rounds = rounds.OrderBy(_ => rnd.Next()).ToList();

            int squadIndex = 1;
            int startHole = 1;

            int n = rounds.Count;
            int maxSize = squadSize;
            int minSize = minAllowed;

            if (n < minSize)
            {
                TempData["Message"] = $"Impossible : {n} joueur(s). Minimum requis = {minSize}.";
                return RedirectToPage(new { competitionId });
            }

            int minSquads = (int)Math.Ceiling(n / (double)maxSize);
            int maxSquads = n / minSize;

            if (minSquads > maxSquads)
            {
                TempData["Message"] = $"Impossible : {n} joueurs ne peuvent pas être répartis en squads de {minSize} à {maxSize}.";
                return RedirectToPage(new { competitionId });
            }

            int k = minSquads;
            int baseSize = n / k;
            int extra = n % k;

            var sizes = new List<int>(k);
            for (int idx = 0; idx < k; idx++)
            {
                int size = baseSize + (idx < extra ? 1 : 0);
                sizes.Add(size);
            }

            if (sizes.Any(s => s < minSize || s > maxSize))
            {
                TempData["Message"] = $"Répartition invalide : {string.Join("-", sizes)}.";
                return RedirectToPage(new { competitionId });
            }

            int i = 0;
            foreach (var take in sizes)
            {
                var chunk = rounds.Skip(i).Take(take).ToList();

                var squad = new Squad
                {
                    CompetitionId = competitionId,
                    Name = $"Squad {squadIndex}",
                    StartHole = startHole
                };

                _db.Squads.Add(squad);
                _db.SaveChanges();

                foreach (var r in chunk)
                    r.SquadId = squad.Id;

                _db.SaveChanges();

                squadIndex++;
                startHole++;
                if (startHole > 18) startHole = 18;

                i += take;
            }

            TempData["Message"] = IsTraining
                ? $"Squads entraînement générés : {squadIndex - 1}."
                : $"Squads générés : {squadIndex - 1}.";

            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostSaveTeams(int competitionId, int squadId, int team1p1, int team1p2, int team2p1, int team2p2)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            if (!IsDoublesMatchPlayType(comp.CompetitionType)
                && comp.CompetitionType != CompetitionType.DoublesScramble
                && comp.CompetitionType != CompetitionType.DoublesFourball
                && comp.CompetitionType != CompetitionType.DoublesFoursome)
            {
                TempData["Message"] = "La composition manuelle des équipes est réservée aux formats doubles.";
                return RedirectToPage(new { competitionId });
            }

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible de modifier les équipes : des scores existent déjà.";
                return RedirectToPage(new { competitionId });
            }

            var squadRounds = _db.Rounds
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == competitionId && r.SquadId == squadId)
                .OrderBy(r => r.Id)
                .ToList();

            if (squadRounds.Count != 4)
            {
                TempData["Message"] = "Il faut exactement 4 joueurs dans le squad pour composer 2 équipes.";
                return RedirectToPage(new { competitionId });
            }

            var squadPlayerIds = squadRounds.Select(r => r.PlayerId).ToHashSet();

            var selectedIds = new List<int> { team1p1, team1p2, team2p1, team2p2 };

            if (selectedIds.Any(id => !squadPlayerIds.Contains(id)))
            {
                TempData["Message"] = "Tous les joueurs sélectionnés doivent appartenir au squad.";
                return RedirectToPage(new { competitionId });
            }

            if (selectedIds.Distinct().Count() != 4)
            {
                TempData["Message"] = "Chaque joueur doit être utilisé une seule fois.";
                return RedirectToPage(new { competitionId });
            }

            var existingMatches = _db.MatchPlayRounds
                .Where(m => m.CompetitionId == competitionId && m.SquadId == squadId)
                .ToList();

            if (existingMatches.Any())
            {
                var matchIds = existingMatches.Select(m => m.Id).ToList();

                var holeResults = _db.MatchPlayHoleResults
                    .Where(h => matchIds.Contains(h.MatchPlayRoundId))
                    .ToList();

                if (holeResults.Any())
                    _db.MatchPlayHoleResults.RemoveRange(holeResults);

                _db.MatchPlayRounds.RemoveRange(existingMatches);
                _db.SaveChanges();
            }

            var existingTeams = _db.Teams
                .Include(t => t.TeamPlayers)
                .Where(t => t.CompetitionId == competitionId && t.SquadId == squadId)
                .ToList();

            if (existingTeams.Any())
            {
                var existingTeamIds = existingTeams.Select(t => t.Id).ToList();

                var existingTeamScores = _db.TeamScores
                    .Include(ts => ts.TeamRound)
                    .Where(ts => ts.TeamRound != null && existingTeamIds.Contains(ts.TeamRound.TeamId))
                    .ToList();

                if (existingTeamScores.Any())
                    _db.TeamScores.RemoveRange(existingTeamScores);

                var existingTeamRounds = _db.TeamRounds
                    .Where(tr => existingTeamIds.Contains(tr.TeamId))
                    .ToList();

                if (existingTeamRounds.Any())
                    _db.TeamRounds.RemoveRange(existingTeamRounds);

                var existingTeamPlayers = _db.TeamPlayers
                    .Where(tp => existingTeamIds.Contains(tp.TeamId))
                    .ToList();

                if (existingTeamPlayers.Any())
                    _db.TeamPlayers.RemoveRange(existingTeamPlayers);

                _db.Teams.RemoveRange(existingTeams);
                _db.SaveChanges();
            }

            string team1Name = BuildTeamName(squadRounds, team1p1, team1p2);
            string team2Name = BuildTeamName(squadRounds, team2p1, team2p2);

            var teamA = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = team1Name,
                IsActive = true
            };

            var teamB = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = team2Name,
                IsActive = true
            };

            _db.Teams.Add(teamA);
            _db.Teams.Add(teamB);
            _db.SaveChanges();

            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamA.Id, PlayerId = team1p1, Order = 1 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamA.Id, PlayerId = team1p2, Order = 2 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamB.Id, PlayerId = team2p1, Order = 1 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamB.Id, PlayerId = team2p2, Order = 2 });

            _db.SaveChanges();

            _db.TeamRounds.Add(new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamA.Id,
                SquadId = squadId,
                IsLocked = false
            });

            _db.TeamRounds.Add(new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamB.Id,
                SquadId = squadId,
                IsLocked = false
            });

            _db.SaveChanges();

            TempData["Message"] = "Équipes enregistrées avec succès.";
            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostGenerateMatchPlay(int competitionId, int squadId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            if (!IsMatchPlayType(comp.CompetitionType))
            {
                TempData["Message"] = "Génération Match Play disponible uniquement pour les compétitions Match Play.";
                return RedirectToPage(new { competitionId });
            }

            if (CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible de générer les matchs : des scores Match Play existent déjà.";
                return RedirectToPage(new { competitionId });
            }

            var squad = _db.Squads.FirstOrDefault(s => s.Id == squadId && s.CompetitionId == competitionId);
            if (squad == null)
            {
                TempData["Message"] = "Squad introuvable.";
                return RedirectToPage(new { competitionId });
            }

            bool alreadyExists = _db.MatchPlayRounds.Any(m => m.CompetitionId == competitionId && m.SquadId == squadId);
            if (alreadyExists)
            {
                TempData["Message"] = "Les matchs existent déjà pour ce squad.";
                return RedirectToPage(new { competitionId });
            }

            if (comp.CompetitionType == CompetitionType.MatchPlayIndividual)
            {
                var squadRounds = _db.Rounds
                    .Include(r => r.Player)
                    .Where(r => r.CompetitionId == competitionId && r.SquadId == squadId)
                    .OrderBy(r => r.Id)
                    .ToList();

                if (squadRounds.Count < 2)
                {
                    TempData["Message"] = "Il faut au moins 2 joueurs dans le squad.";
                    return RedirectToPage(new { competitionId });
                }

                if (squadRounds.Count % 2 != 0)
                {
                    TempData["Message"] = "Le Match Play individuel nécessite un nombre pair de joueurs dans le squad.";
                    return RedirectToPage(new { competitionId });
                }

                for (int i = 0; i < squadRounds.Count; i += 2)
                {
                    var roundA = squadRounds[i];
                    var roundB = squadRounds[i + 1];

                    var teamA = new Team
                    {
                        CompetitionId = competitionId,
                        SquadId = squadId,
                        Name = $"{roundA.Player?.FirstName} {roundA.Player?.LastName}".Trim(),
                        IsActive = true
                    };

                    var teamB = new Team
                    {
                        CompetitionId = competitionId,
                        SquadId = squadId,
                        Name = $"{roundB.Player?.FirstName} {roundB.Player?.LastName}".Trim(),
                        IsActive = true
                    };

                    _db.Teams.Add(teamA);
                    _db.Teams.Add(teamB);
                    _db.SaveChanges();

                    _db.TeamPlayers.Add(new TeamPlayer
                    {
                        TeamId = teamA.Id,
                        PlayerId = roundA.PlayerId,
                        Order = 1
                    });

                    _db.TeamPlayers.Add(new TeamPlayer
                    {
                        TeamId = teamB.Id,
                        PlayerId = roundB.PlayerId,
                        Order = 1
                    });

                    _db.SaveChanges();

                    var teamRoundA = new TeamRound
                    {
                        CompetitionId = competitionId,
                        TeamId = teamA.Id,
                        SquadId = squadId,
                        IsLocked = false
                    };

                    var teamRoundB = new TeamRound
                    {
                        CompetitionId = competitionId,
                        TeamId = teamB.Id,
                        SquadId = squadId,
                        IsLocked = false
                    };

                    _db.TeamRounds.Add(teamRoundA);
                    _db.TeamRounds.Add(teamRoundB);
                    _db.SaveChanges();

                    _db.MatchPlayRounds.Add(new MatchPlayRound
                    {
                        CompetitionId = competitionId,
                        SquadId = squadId,
                        TeamAId = teamA.Id,
                        TeamBId = teamB.Id,
                        CurrentHole = 1,
                        IsFinished = false,
                        StatusText = "AS",
                        ResultText = ""
                    });
                }

                _db.SaveChanges();

                TempData["Message"] = "Matchs Match Play générés avec succès.";
                return RedirectToPage(new { competitionId });
            }

            if (IsDoublesMatchPlayType(comp.CompetitionType))
            {
                var teams = _db.Teams
                    .Include(t => t.TeamPlayers)
                        .ThenInclude(tp => tp.Player)
                    .Where(t => t.CompetitionId == competitionId && t.SquadId == squadId && t.IsActive)
                    .OrderBy(t => t.Id)
                    .ToList();

                if (teams.Count != 2)
                {
                    var squadRounds = _db.Rounds
                        .Include(r => r.Player)
                        .Where(r => r.CompetitionId == competitionId && r.SquadId == squadId)
                        .OrderBy(r => r.Id)
                        .ToList();

                    if (squadRounds.Count != 4)
                    {
                        TempData["Message"] = "Le Match Play doubles nécessite 4 joueurs dans le squad.";
                        return RedirectToPage(new { competitionId });
                    }

                    CreateOrReplaceAutoDoublesTeams(competitionId, squadId, squadRounds);

                    teams = _db.Teams
                        .Include(t => t.TeamPlayers)
                            .ThenInclude(tp => tp.Player)
                        .Where(t => t.CompetitionId == competitionId && t.SquadId == squadId && t.IsActive)
                        .OrderBy(t => t.Id)
                        .ToList();
                }

                if (teams.Count != 2)
                {
                    TempData["Message"] = "Impossible de créer ou retrouver les 2 équipes pour le Match Play doubles.";
                    return RedirectToPage(new { competitionId });
                }

                foreach (var team in teams)
                {
                    bool hasTeamRound = _db.TeamRounds.Any(tr =>
                        tr.CompetitionId == competitionId &&
                        tr.SquadId == squadId &&
                        tr.TeamId == team.Id);

                    if (!hasTeamRound)
                    {
                        _db.TeamRounds.Add(new TeamRound
                        {
                            CompetitionId = competitionId,
                            TeamId = team.Id,
                            SquadId = squadId,
                            IsLocked = false
                        });
                    }
                }

                _db.SaveChanges();

                _db.MatchPlayRounds.Add(new MatchPlayRound
                {
                    CompetitionId = competitionId,
                    SquadId = squadId,
                    TeamAId = teams[0].Id,
                    TeamBId = teams[1].Id,
                    CurrentHole = 1,
                    IsFinished = false,
                    StatusText = "AS",
                    ResultText = ""
                });

                _db.SaveChanges();

                TempData["Message"] = "Match doubles Match Play généré avec succès.";
                return RedirectToPage(new { competitionId });
            }

            TempData["Message"] = "Type de Match Play non géré.";
            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostCreateSquad(int competitionId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null)
                return RedirectToPage("/Competitions");

            if (!IsTrainingMode(comp))
            {
                TempData["Message"] = "Création manuelle disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            int nextIndex = _db.Squads.Count(s => s.CompetitionId == competitionId) + 1;

            var squad = new Squad
            {
                CompetitionId = competitionId,
                Name = $"Squad {nextIndex}",
                StartHole = 1
            };

            _db.Squads.Add(squad);
            _db.SaveChanges();

            TempData["Message"] = $"Squad créé : {squad.Name}";
            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostAssign(int competitionId, int roundId, int squadId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null)
                return RedirectToPage("/Competitions");

            if (!IsTrainingMode(comp))
            {
                TempData["Message"] = "Affectation manuelle disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            var round = _db.Rounds.Include(r => r.Player).FirstOrDefault(r => r.Id == roundId);
            if (round == null || round.CompetitionId != competitionId)
            {
                TempData["Message"] = "Round introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            var squad = _db.Squads.FirstOrDefault(s => s.Id == squadId);
            if (squad == null || squad.CompetitionId != competitionId)
            {
                TempData["Message"] = "Squad introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            var (minAllowed, maxAllowed) = SquadRules.GetLimits(comp);
            int currentCount = _db.Rounds.Count(r => r.CompetitionId == competitionId && r.SquadId == squadId);

            if (currentCount >= maxAllowed)
            {
                TempData["Message"] = $"Squad plein : maximum {maxAllowed} joueur(s).";
                return RedirectToPage(new { competitionId });
            }

            round.SquadId = squadId;
            _db.SaveChanges();

            TempData["Message"] = $"{round.Player?.FirstName} {round.Player?.LastName} affecté à {squad.Name}.";
            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostRemove(int competitionId, int roundId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions.AsNoTracking().FirstOrDefault(c => c.Id == competitionId);
            if (comp == null)
                return RedirectToPage("/Competitions");

            if (!IsTrainingMode(comp))
            {
                TempData["Message"] = "Retrait manuel disponible uniquement en entraînement.";
                return RedirectToPage(new { competitionId });
            }

            var round = _db.Rounds.Include(r => r.Player).FirstOrDefault(r => r.Id == roundId);
            if (round == null || round.CompetitionId != competitionId)
            {
                TempData["Message"] = "Round introuvable ou invalide.";
                return RedirectToPage(new { competitionId });
            }

            round.SquadId = null;
            _db.SaveChanges();

            TempData["Message"] = $"{round.Player?.FirstName} {round.Player?.LastName} retiré du squad.";
            return RedirectToPage(new { competitionId });
        }

        public IActionResult OnPostClear(int competitionId)
        {
            this.competitionId = competitionId;

            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            if (comp == null)
                return RedirectToPage("/Competitions");

            bool isTraining = IsTrainingMode(comp);

            if (!isTraining && CompetitionHasScores(competitionId))
            {
                TempData["Message"] = "Impossible de réinitialiser : des scores existent déjà pour cette compétition.";
                return RedirectToPage(new { competitionId });
            }

            if (isTraining)
            {
                var roundIds = _db.Rounds
                    .Where(r => r.CompetitionId == competitionId)
                    .Select(r => r.Id)
                    .ToList();

                var scores = _db.Scores
                    .Where(s => roundIds.Contains(s.RoundId))
                    .ToList();

                if (scores.Any())
                    _db.Scores.RemoveRange(scores);

                var roundsAll = _db.Rounds
                    .Where(r => r.CompetitionId == competitionId)
                    .ToList();

                foreach (var r in roundsAll)
                {
                    r.SquadId = null;
                    r.IsLocked = false;
                }

                var squadsAll = _db.Squads
                    .Where(s => s.CompetitionId == competitionId)
                    .ToList();

                if (squadsAll.Any())
                    _db.Squads.RemoveRange(squadsAll);

                _db.SaveChanges();

                TempData["Message"] = "Session d'entraînement réinitialisée.";
                return RedirectToPage(new { competitionId });
            }

            var matchPlayResults = _db.MatchPlayHoleResults
                .Include(h => h.MatchPlayRound)
                .Where(h => h.MatchPlayRound != null && h.MatchPlayRound.CompetitionId == competitionId)
                .ToList();

            if (matchPlayResults.Any())
            {
                _db.MatchPlayHoleResults.RemoveRange(matchPlayResults);
                _db.SaveChanges();
            }

            var matchPlayRounds = _db.MatchPlayRounds
                .Where(m => m.CompetitionId == competitionId)
                .ToList();

            if (matchPlayRounds.Any())
            {
                _db.MatchPlayRounds.RemoveRange(matchPlayRounds);
                _db.SaveChanges();
            }

            var teamRounds = _db.TeamRounds
                .Where(tr => tr.CompetitionId == competitionId)
                .ToList();

            if (teamRounds.Any())
            {
                _db.TeamRounds.RemoveRange(teamRounds);
                _db.SaveChanges();
            }

            var teams = _db.Teams
                .Where(t => t.CompetitionId == competitionId)
                .ToList();

            if (teams.Any())
            {
                var teamIds = teams.Select(t => t.Id).ToList();

                var teamPlayers = _db.TeamPlayers
                    .Where(tp => teamIds.Contains(tp.TeamId))
                    .ToList();

                if (teamPlayers.Any())
                {
                    _db.TeamPlayers.RemoveRange(teamPlayers);
                    _db.SaveChanges();
                }

                _db.Teams.RemoveRange(teams);
                _db.SaveChanges();
            }

            var squads = _db.Squads
                .Where(s => s.CompetitionId == competitionId)
                .ToList();

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == competitionId && r.SquadId != null)
                .ToList();

            foreach (var r in rounds)
                r.SquadId = null;

            _db.SaveChanges();

            if (squads.Any())
            {
                _db.Squads.RemoveRange(squads);
                _db.SaveChanges();
            }

            TempData["Message"] = "Squads réinitialisés.";
            return RedirectToPage(new { competitionId });
        }

        private bool CompetitionHasScores(int compId)
        {
            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == compId);

            if (comp == null)
                return false;

            if (IsMatchPlayType(comp.CompetitionType))
            {
                return _db.MatchPlayHoleResults
                    .Include(h => h.MatchPlayRound)
                    .Any(h => h.MatchPlayRound != null && h.MatchPlayRound.CompetitionId == compId);
            }

            return _db.Scores
                .Include(s => s.Round)
                .Any(s => s.Round != null && s.Round.CompetitionId == compId && s.Strokes > 0);
        }

        private bool IsTrainingMode(Competition comp)
        {
            return string.Equals(comp.Mode, "Training", StringComparison.OrdinalIgnoreCase)
                   || comp.ScoringMode == ScoringMode.IndividualAllowed;
        }

        private bool IsMatchPlayType(CompetitionType type)
        {
            return type == CompetitionType.MatchPlayIndividual
                   || type == CompetitionType.MatchPlayFourball
                   || type == CompetitionType.MatchPlayFoursome
                   || type == CompetitionType.MatchPlayScramble;
        }

        private bool IsDoublesMatchPlayType(CompetitionType type)
        {
            return type == CompetitionType.MatchPlayFourball
                   || type == CompetitionType.MatchPlayFoursome
                   || type == CompetitionType.MatchPlayScramble;
        }

        private string BuildTeamName(List<Round> squadRounds, int playerId1, int playerId2)
        {
            string p1 = squadRounds
                .Where(r => r.PlayerId == playerId1)
                .Select(r => $"{r.Player!.FirstName} {r.Player.LastName}".Trim())
                .FirstOrDefault() ?? $"PlayerId={playerId1}";

            string p2 = squadRounds
                .Where(r => r.PlayerId == playerId2)
                .Select(r => $"{r.Player!.FirstName} {r.Player.LastName}".Trim())
                .FirstOrDefault() ?? $"PlayerId={playerId2}";

            return $"{p1} / {p2}";
        }

        private void CreateOrReplaceAutoDoublesTeams(int competitionId, int squadId, List<Round> squadRounds)
        {
            var existingTeams = _db.Teams
                .Where(t => t.CompetitionId == competitionId && t.SquadId == squadId)
                .ToList();

            if (existingTeams.Any())
            {
                var existingTeamIds = existingTeams.Select(t => t.Id).ToList();

                var existingTeamScores = _db.TeamScores
                    .Include(ts => ts.TeamRound)
                    .Where(ts => ts.TeamRound != null && existingTeamIds.Contains(ts.TeamRound.TeamId))
                    .ToList();

                if (existingTeamScores.Any())
                    _db.TeamScores.RemoveRange(existingTeamScores);

                var existingTeamRounds = _db.TeamRounds
                    .Where(tr => existingTeamIds.Contains(tr.TeamId))
                    .ToList();

                if (existingTeamRounds.Any())
                    _db.TeamRounds.RemoveRange(existingTeamRounds);

                var existingTeamPlayers = _db.TeamPlayers
                    .Where(tp => existingTeamIds.Contains(tp.TeamId))
                    .ToList();

                if (existingTeamPlayers.Any())
                    _db.TeamPlayers.RemoveRange(existingTeamPlayers);

                _db.Teams.RemoveRange(existingTeams);
                _db.SaveChanges();
            }

            var teamA = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = BuildTeamName(squadRounds, squadRounds[0].PlayerId, squadRounds[1].PlayerId),
                IsActive = true
            };

            var teamB = new Team
            {
                CompetitionId = competitionId,
                SquadId = squadId,
                Name = BuildTeamName(squadRounds, squadRounds[2].PlayerId, squadRounds[3].PlayerId),
                IsActive = true
            };

            _db.Teams.Add(teamA);
            _db.Teams.Add(teamB);
            _db.SaveChanges();

            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamA.Id, PlayerId = squadRounds[0].PlayerId, Order = 1 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamA.Id, PlayerId = squadRounds[1].PlayerId, Order = 2 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamB.Id, PlayerId = squadRounds[2].PlayerId, Order = 1 });
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = teamB.Id, PlayerId = squadRounds[3].PlayerId, Order = 2 });

            _db.SaveChanges();

            _db.TeamRounds.Add(new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamA.Id,
                SquadId = squadId,
                IsLocked = false
            });

            _db.TeamRounds.Add(new TeamRound
            {
                CompetitionId = competitionId,
                TeamId = teamB.Id,
                SquadId = squadId,
                IsLocked = false
            });

            _db.SaveChanges();
        }

        private void LoadSquadsAndUnassigned()
        {
            var comp = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == competitionId);

            CompetitionName = comp?.Name ?? "";
            IsMatchPlayIndividual = comp?.CompetitionType == CompetitionType.MatchPlayIndividual;
            IsMatchPlayCompetition = comp != null && IsMatchPlayType(comp.CompetitionType);
            IsMatchPlayDoubles = comp != null && IsDoublesMatchPlayType(comp.CompetitionType);

            var squads = _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == competitionId)
                .OrderBy(s => s.Id)
                .ToList();

            var rounds = _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == competitionId)
                .ToList();

            var allMatchPlayRounds = new List<MatchPlayRound>();

            if (IsMatchPlayCompetition)
            {
                allMatchPlayRounds = _db.MatchPlayRounds
                    .Include(m => m.TeamA)
                        .ThenInclude(t => t.TeamPlayers)
                            .ThenInclude(tp => tp.Player)
                    .Include(m => m.TeamB)
                        .ThenInclude(t => t.TeamPlayers)
                            .ThenInclude(tp => tp.Player)
                    .Where(m => m.CompetitionId == competitionId)
                    .OrderBy(m => m.Id)
                    .ToList();
            }

            var allTeams = _db.Teams
                .AsNoTracking()
                .Include(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                .Where(t => t.CompetitionId == competitionId)
                .OrderBy(t => t.Id)
                .ToList();

            Squads = squads.Select(s => new SquadView
            {
                Id = s.Id,
                Name = s.Name,
                StartHole = s.StartHole,
                Rounds = rounds.Where(r => r.SquadId == s.Id).ToList(),
                MatchPlayRounds = allMatchPlayRounds.Where(m => m.SquadId == s.Id).ToList(),
                Teams = allTeams
                    .Where(t => t.SquadId == s.Id && t.IsActive)
                    .Select(t => new TeamView
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Player1Id = t.TeamPlayers.OrderBy(tp => tp.Order).Select(tp => (int?)tp.PlayerId).FirstOrDefault(),
                        Player2Id = t.TeamPlayers.OrderBy(tp => tp.Order).Skip(1).Select(tp => (int?)tp.PlayerId).FirstOrDefault(),
                        DisplayName = t.TeamPlayers.Any()
                            ? string.Join(" / ",
                                t.TeamPlayers
                                    .OrderBy(tp => tp.Order)
                                    .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}".Trim()))
                            : t.Name
                    })
                    .ToList()
            }).ToList();

            UnassignedRounds = rounds
                .Where(r => r.SquadId == null)
                .OrderBy(r => r.Player!.LastName)
                .ThenBy(r => r.Player!.FirstName)
                .ToList();

            if (comp != null)
            {
                IsTraining = IsTrainingMode(comp);
                var (min, max) = SquadRules.GetLimits(comp);
                MinSquadSize = min;
                MaxSquadSize = max;

                HasScores = CompetitionHasScores(competitionId);
                CanEditSquads = IsTraining || !HasScores;
                LockMessage = (!IsTraining && HasScores)
                    ? "Squads verrouillés : la compétition a déjà démarré."
                    : "";
            }
        }
    }
}