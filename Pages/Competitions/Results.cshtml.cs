using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class ResultsModel : PageModel
    {
        private readonly AppDbContext _db;

        public ResultsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<CompetitionRow> Competitions { get; set; } = new();

        public class CompetitionRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime Date { get; set; }
            public string Mode { get; set; } = "";
            public string CourseName { get; set; } = "";
            public int PlayerCount { get; set; }
            public bool HasStarted { get; set; }
            public bool IsFinished { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var scope = await GetCompetitionListScopeAsync();
            if (scope == null)
                return Forbid();

            var competitionsQuery = _db.Competitions
                .AsNoTracking()
                .AsQueryable();

            if (!scope.IsAdmin)
            {
                competitionsQuery = competitionsQuery
                    .Where(competition => competition.ClubId == scope.ClubId);
            }

            var competitions = await competitionsQuery
                .Include(c => c.Course)
                .OrderByDescending(c => c.Date)
                .ToListAsync();

            var rows = new List<CompetitionRow>();

            foreach (var comp in competitions)
            {
                var rounds = await _db.Rounds
                    .AsNoTracking()
                    .Where(r => r.CompetitionId == comp.Id)
                    .ToListAsync();

                var roundIds = rounds.Select(r => r.Id).ToList();

                var scores = await _db.Scores
                    .AsNoTracking()
                    .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                    .ToListAsync();

                int players = rounds.Count;
                bool hasStarted = scores.Any();
                bool isFinished = false;

                if (players > 0)
                {
                    int completedRounds = 0;

                    foreach (var round in rounds)
                    {
                        int playedHoles = scores.Count(s => s.RoundId == round.Id);
                        if (playedHoles >= 18)
                            completedRounds++;
                    }

                    isFinished = completedRounds == players;
                }

                rows.Add(new CompetitionRow
                {
                    Id = comp.Id,
                    Name = comp.Name,
                    Date = comp.Date,
                    Mode = comp.Mode,
                    CourseName = comp.Course?.Name ?? "-",
                    PlayerCount = players,
                    HasStarted = hasStarted,
                    IsFinished = isFinished
                });
            }

            Competitions = rows;
            return Page();
        }

        private async Task<CompetitionListScope?> GetCompetitionListScopeAsync()
        {
            if (User.Identity?.IsAuthenticated != true
                || !int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var userId))
            {
                return null;
            }

            var currentUser = await _db.AppUsers
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Role,
                    user.ClubId
                })
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            if (currentUser == null)
                return null;

            if (string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return new CompetitionListScope(true, null);

            if (string.Equals(currentUser.Role, "Club", StringComparison.OrdinalIgnoreCase)
                && currentUser.ClubId.HasValue)
            {
                return new CompetitionListScope(false, currentUser.ClubId.Value);
            }

            return null;
        }

        private sealed record CompetitionListScope(bool IsAdmin, int? ClubId);
    }
}
