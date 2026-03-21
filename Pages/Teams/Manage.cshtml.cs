using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Teams
{
    [Authorize(Roles = "Admin,Organizer")]
    public class ManageModel : PageModel
    {
        private readonly AppDbContext _db;

        public ManageModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        public Competition? Competition { get; set; }

        public List<Player> AvailablePlayers { get; set; } = new();
        public List<TeamViewModel> Teams { get; set; } = new();
        public List<Squad> ExistingSquads { get; set; } = new();

        [BindProperty]
        public string TeamName { get; set; } = string.Empty;

        [BindProperty]
        public int Player1Id { get; set; }

        [BindProperty]
        public int Player2Id { get; set; }

        [BindProperty]
        public int TeamsPerSquad { get; set; } = 2;

        public bool IsDoublesCompetition { get; set; }

        public class TeamViewModel
        {
            public int TeamId { get; set; }
            public string TeamName { get; set; } = string.Empty;
            public string PlayersDisplay { get; set; } = string.Empty;
            public int PlayerCount { get; set; }
            public string SquadDisplay { get; set; } = "-";
        }

        public IActionResult OnGet()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition)
            {
                TempData["Error"] = "Cette page est réservée aux compétitions en doubles.";
                return RedirectToPage("/Competitions");
            }

            return Page();
        }

        public IActionResult OnPostAdd()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition)
            {
                TempData["Error"] = "Cette page est réservée aux compétitions en doubles.";
                return RedirectToPage("/Competitions");
            }

            if (Player1Id <= 0 || Player2Id <= 0)
                ModelState.AddModelError(string.Empty, "Sélectionne les 2 joueurs.");

            if (Player1Id == Player2Id)
                ModelState.AddModelError(string.Empty, "Une équipe doit contenir 2 joueurs différents.");

            var allowedPlayerIds = GetParticipantPlayerIds();

            if (!allowedPlayerIds.Contains(Player1Id) || !allowedPlayerIds.Contains(Player2Id))
                ModelState.AddModelError(string.Empty, "Les joueurs doivent être participants à la compétition.");

            var player1 = _db.Players.FirstOrDefault(p => p.Id == Player1Id && p.IsActive);
            var player2 = _db.Players.FirstOrDefault(p => p.Id == Player2Id && p.IsActive);

            if (player1 == null || player2 == null)
                ModelState.AddModelError(string.Empty, "Un ou plusieurs joueurs sont introuvables.");

            bool player1AlreadyUsed = _db.TeamPlayers
                .Include(tp => tp.Team)
                .Any(tp => tp.PlayerId == Player1Id && tp.Team.CompetitionId == CompetitionId);

            bool player2AlreadyUsed = _db.TeamPlayers
                .Include(tp => tp.Team)
                .Any(tp => tp.PlayerId == Player2Id && tp.Team.CompetitionId == CompetitionId);

            if (player1AlreadyUsed)
                ModelState.AddModelError(string.Empty, "Le joueur 1 est déjà dans une équipe de cette compétition.");

            if (player2AlreadyUsed)
                ModelState.AddModelError(string.Empty, "Le joueur 2 est déjà dans une équipe de cette compétition.");

            if (!ModelState.IsValid)
                return Page();

            string finalTeamName = BuildAutomaticTeamName(player1!, player2!);

            if (!string.IsNullOrWhiteSpace(TeamName))
                finalTeamName = TeamName.Trim();

            var team = new Team
            {
                CompetitionId = CompetitionId,
                Name = finalTeamName,
                IsActive = true
            };

            _db.Teams.Add(team);
            _db.SaveChanges();

            _db.TeamPlayers.Add(new TeamPlayer
            {
                TeamId = team.Id,
                PlayerId = Player1Id,
                Order = 1
            });

            _db.TeamPlayers.Add(new TeamPlayer
            {
                TeamId = team.Id,
                PlayerId = Player2Id,
                Order = 2
            });

            _db.SaveChanges();

            TempData["SuccessMessage"] = $"Équipe créée : {team.Name}";
            return RedirectToPage(new { competitionId = CompetitionId });
        }

        public IActionResult OnPostGenerateTeams()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition)
            {
                TempData["Error"] = "Cette page est réservée aux compétitions en doubles.";
                return RedirectToPage("/Competitions");
            }

            var participantPlayerIds = GetParticipantPlayerIds();

            if (!participantPlayerIds.Any())
            {
                TempData["Error"] = "Aucun participant trouvé pour cette compétition.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            var alreadyUsedPlayerIds = _db.TeamPlayers
                .Include(tp => tp.Team)
                .Where(tp => tp.Team.CompetitionId == CompetitionId)
                .Select(tp => tp.PlayerId)
                .ToHashSet();

            var freePlayers = _db.Players
                .Where(p => participantPlayerIds.Contains(p.Id) && p.IsActive && !alreadyUsedPlayerIds.Contains(p.Id))
                .ToList();

            freePlayers = freePlayers
                .OrderBy(p => Guid.NewGuid())
                .ToList();

            if (freePlayers.Count < 2)
            {
                TempData["Error"] = "Pas assez de joueurs disponibles pour générer de nouvelles équipes.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            if (freePlayers.Count % 2 != 0)
            {
                TempData["Error"] = "Nombre impair de joueurs disponibles. Impossible de générer toutes les équipes automatiquement.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            int created = 0;

            for (int i = 0; i < freePlayers.Count; i += 2)
            {
                var p1 = freePlayers[i];
                var p2 = freePlayers[i + 1];

                var team = new Team
                {
                    CompetitionId = CompetitionId,
                    Name = BuildAutomaticTeamName(p1, p2),
                    IsActive = true
                };

                _db.Teams.Add(team);
                _db.SaveChanges();

                _db.TeamPlayers.Add(new TeamPlayer
                {
                    TeamId = team.Id,
                    PlayerId = p1.Id,
                    Order = 1
                });

                _db.TeamPlayers.Add(new TeamPlayer
                {
                    TeamId = team.Id,
                    PlayerId = p2.Id,
                    Order = 2
                });

                _db.SaveChanges();
                created++;
            }

            TempData["SuccessMessage"] = $"{created} équipe(s) générée(s) automatiquement.";
            return RedirectToPage(new { competitionId = CompetitionId });
        }

        public IActionResult OnPostGenerateSquads()
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition)
            {
                TempData["Error"] = "Cette page est réservée aux compétitions en doubles.";
                return RedirectToPage("/Competitions");
            }

            if (TeamsPerSquad < 2 || TeamsPerSquad > 4)
            {
                TempData["Error"] = "Le nombre d'équipes par squad doit être compris entre 2 et 4.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            var teams = _db.Teams
                .Where(t => t.CompetitionId == CompetitionId)
                .OrderBy(t => t.Name)
                .ToList();

            if (!teams.Any())
            {
                TempData["Error"] = "Aucune équipe à affecter.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            var oldSquads = _db.Squads
                .Where(s => s.CompetitionId == CompetitionId)
                .ToList();

            if (oldSquads.Any())
            {
                foreach (var team in teams)
                    team.SquadId = null;

                _db.SaveChanges();

                _db.Squads.RemoveRange(oldSquads);
                _db.SaveChanges();
            }

            int squadIndex = 1;
            int startHole = 1;

            for (int i = 0; i < teams.Count; i += TeamsPerSquad)
            {
                var chunk = teams.Skip(i).Take(TeamsPerSquad).ToList();

                var squad = new Squad
                {
                    CompetitionId = CompetitionId,
                    Name = $"Squad {squadIndex}",
                    StartHole = startHole,
                    StartTime = null
                };

                _db.Squads.Add(squad);
                _db.SaveChanges();

                foreach (var team in chunk)
                    team.SquadId = squad.Id;

                _db.SaveChanges();

                squadIndex++;
                startHole++;
                if (startHole > 18)
                    startHole = 1;
            }

            var teamsWithSquad = _db.Teams
                .Where(t => t.CompetitionId == CompetitionId)
                .ToList();

            foreach (var team in teamsWithSquad)
            {
                var existingTeamRound = _db.TeamRounds
                    .FirstOrDefault(tr => tr.TeamId == team.Id && tr.CompetitionId == CompetitionId);

                if (existingTeamRound == null)
                {
                    _db.TeamRounds.Add(new TeamRound
                    {
                        CompetitionId = CompetitionId,
                        TeamId = team.Id,
                        SquadId = team.SquadId,
                        IsLocked = false
                    });
                }
                else
                {
                    existingTeamRound.SquadId = team.SquadId;
                }
            }

            _db.SaveChanges();

            TempData["SuccessMessage"] = $"Squads générés automatiquement ({TeamsPerSquad} équipe(s) par squad).";
            return RedirectToPage(new { competitionId = CompetitionId });
        }

        public IActionResult OnPostDelete(int teamId)
        {
            if (!LoadPageData())
                return RedirectToPage("/Competitions");

            if (!IsDoublesCompetition)
            {
                TempData["Error"] = "Cette page est réservée aux compétitions en doubles.";
                return RedirectToPage("/Competitions");
            }

            var team = _db.Teams
                .Include(t => t.TeamPlayers)
                .Include(t => t.TeamRounds)
                .FirstOrDefault(t => t.Id == teamId && t.CompetitionId == CompetitionId);

            if (team == null)
            {
                TempData["Error"] = "Équipe introuvable.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            if (team.TeamRounds.Any())
            {
                TempData["Error"] = "Impossible de supprimer cette équipe : une carte équipe existe déjà.";
                return RedirectToPage(new { competitionId = CompetitionId });
            }

            if (team.TeamPlayers.Any())
                _db.TeamPlayers.RemoveRange(team.TeamPlayers);

            _db.Teams.Remove(team);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Équipe supprimée.";
            return RedirectToPage(new { competitionId = CompetitionId });
        }

        private bool LoadPageData()
        {
            Competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return false;

            IsDoublesCompetition =
                Competition.CompetitionType == CompetitionType.DoublesScramble ||
                Competition.CompetitionType == CompetitionType.DoublesFourball ||
                Competition.CompetitionType == CompetitionType.DoublesFoursome;

            var participantIds = GetParticipantPlayerIds();

            AvailablePlayers = _db.Players
                .AsNoTracking()
                .Where(p => p.IsActive && participantIds.Contains(p.Id))
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();

            ExistingSquads = _db.Squads
                .AsNoTracking()
                .Where(s => s.CompetitionId == CompetitionId)
                .OrderBy(s => s.Name)
                .ToList();

            Teams = _db.Teams
                .AsNoTracking()
                .Include(t => t.Squad)
                .Include(t => t.TeamPlayers)
                    .ThenInclude(tp => tp.Player)
                .Where(t => t.CompetitionId == CompetitionId)
                .OrderBy(t => t.Name)
                .Select(t => new TeamViewModel
                {
                    TeamId = t.Id,
                    TeamName = t.Name,
                    PlayerCount = t.TeamPlayers.Count,
                    PlayersDisplay = string.Join(" / ",
                        t.TeamPlayers
                            .OrderBy(tp => tp.Order)
                            .Select(tp => $"{tp.Player.FirstName} {tp.Player.LastName}")),
                    SquadDisplay = t.Squad != null ? t.Squad.Name : "-"
                })
                .ToList();

            return true;
        }

        private HashSet<int> GetParticipantPlayerIds()
        {
            return _db.Rounds
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId)
                .Select(r => r.PlayerId)
                .Distinct()
                .ToHashSet();
        }

        private string BuildAutomaticTeamName(Player p1, Player p2)
        {
            return $"{p1.FirstName} {p1.LastName} / {p2.FirstName} {p2.LastName}";
        }
    }
}