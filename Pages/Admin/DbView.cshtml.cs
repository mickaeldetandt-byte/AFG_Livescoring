using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DbViewModel : PageModel
    {
        private readonly AppDbContext _db;

        public DbViewModel(AppDbContext db)
        {
            _db = db;
        }

        public string DbPath { get; set; } = "";

        public List<string> Tables { get; set; } = new();

        public int PlayersCount { get; set; }
        public int CompetitionsCount { get; set; }
        public int RoundsCount { get; set; }
        public int ScoresCount { get; set; }
        public int SquadsCount { get; set; }

        public List<Player> Players { get; set; } = new();
        public List<Competition> Competitions { get; set; } = new();
        public List<Round> Rounds { get; set; } = new();
        public List<Score> Scores { get; set; } = new();

        public List<Squad> Squads { get; set; } = new();

        public void OnGet()
        {
            // DB path réel
            DbPath = _db.Database.GetDbConnection().DataSource;

            // Liste des tables SQLite
            using (var con = new SqliteConnection($"Data Source={DbPath}"))
            {
                con.Open();
                var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    Tables.Add(reader.GetString(0));
            }

            Players = _db.Players
                .AsNoTracking()
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();

            Competitions = _db.Competitions
                .AsNoTracking()
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.Name)
                .ToList();

            Rounds = _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .Include(r => r.Squad)
                .OrderByDescending(r => r.Id)
                .ToList();

            Scores = _db.Scores
                .AsNoTracking()
                .Include(s => s.Round)
                .ThenInclude(r => r.Player)
                .Include(s => s.Round)
                .ThenInclude(r => r.Competition)
                .OrderByDescending(s => s.Id)
                .Take(200)
                .ToList();

            Squads = _db.Squads
                .AsNoTracking()
                .Include(s => s.Competition)
                .OrderByDescending(s => s.Id)
                .ToList();

            ScoresCount = Scores.Count;
            PlayersCount = Players.Count;
            CompetitionsCount = Competitions.Count;
            RoundsCount = Rounds.Count;
            SquadsCount = Squads.Count;
        }
    }
}