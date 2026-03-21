using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class ResultsDetailsModel : PageModel
    {
        private readonly AppDbContext _db;

        public ResultsDetailsModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        public Competition? Competition { get; set; }

        public List<ResultRow> Results { get; set; } = new();

        public int PlayerCount => Results.Count;
        public double AverageScore => Results.Any() ? Results.Average(r => r.TotalStrokes) : 0;
        public int BestScore => Results.Any() ? Results.Min(r => r.TotalStrokes) : 0;
        public int CompletedPlayers => Results.Count(r => r.IsComplete);

        public class ResultRow
        {
            public int Rank { get; set; }
            public int RoundId { get; set; }
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public string SquadName { get; set; } = "";
            public int HolesPlayed { get; set; }
            public int TotalStrokes { get; set; }
            public int TotalParPlayed { get; set; }
            public int ToPar => TotalStrokes - TotalParPlayed;
            public bool IsComplete { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var ok = await LoadResultsAsync();
            if (!ok)
                return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            var ok = await LoadResultsAsync();
            if (!ok || Competition == null)
                return NotFound();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Résultats");

            ws.Cell(1, 1).Value = "AFG LiveScoring";
            ws.Cell(2, 1).Value = $"Compétition : {Competition.Name}";
            ws.Cell(3, 1).Value = $"Date : {Competition.Date:dd/MM/yyyy}";

            ws.Range(1, 1, 1, 8).Merge();
            ws.Range(2, 1, 2, 8).Merge();
            ws.Range(3, 1, 3, 8).Merge();

            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;

            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 13;

            ws.Cell(3, 1).Style.Font.Italic = true;

            ws.Cell(5, 1).Value = "Joueurs";
            ws.Cell(5, 2).Value = PlayerCount;
            ws.Cell(5, 3).Value = "Terminés";
            ws.Cell(5, 4).Value = CompletedPlayers;
            ws.Cell(5, 5).Value = "Score moyen";
            ws.Cell(5, 6).Value = AverageScore;
            ws.Cell(5, 7).Value = "Meilleur score";
            ws.Cell(5, 8).Value = BestScore;

            ws.Range(5, 1, 5, 8).Style.Font.Bold = true;
            ws.Range(5, 1, 5, 8).Style.Fill.BackgroundColor = XLColor.LightBlue;

            int row = 7;
            ws.Cell(row, 1).Value = "Rang";
            ws.Cell(row, 2).Value = "Joueur";
            ws.Cell(row, 3).Value = "Squad";
            ws.Cell(row, 4).Value = "Trous joués";
            ws.Cell(row, 5).Value = "Coups";
            ws.Cell(row, 6).Value = "Par joué";
            ws.Cell(row, 7).Value = "À Par";
            ws.Cell(row, 8).Value = "Statut";

            var header = ws.Range(row, 1, row, 8);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;

            foreach (var item in Results)
            {
                ws.Cell(row, 1).Value = item.Rank;
                ws.Cell(row, 2).Value = item.PlayerName;
                ws.Cell(row, 3).Value = item.SquadName;
                ws.Cell(row, 4).Value = item.HolesPlayed;
                ws.Cell(row, 5).Value = item.TotalStrokes;
                ws.Cell(row, 6).Value = item.TotalParPlayed;
                ws.Cell(row, 7).Value = FormatToPar(item.ToPar);
                ws.Cell(row, 8).Value = item.IsComplete ? "Terminé" : "Incomplet";

                if (item.Rank == 1)
                    ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.LightYellow;
                else if (item.Rank == 2)
                    ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.LightGray;
                else if (item.Rank == 3)
                    ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F4C7A1");

                row++;
            }

            var tableRange = ws.Range(7, 1, row - 1, 8);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Range(8, 1, row - 1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(8, 4, row - 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Column(6).Style.NumberFormat.Format = "0";
            ws.Column(5).Style.NumberFormat.Format = "0";
            ws.Column(4).Style.NumberFormat.Format = "0";
            ws.Column(1).Style.NumberFormat.Format = "0";
            ws.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"resultats_competition_{Competition.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        private async Task<bool> LoadResultsAsync()
        {
            Competition = await _db.Competitions
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == CompetitionId);

            if (Competition == null)
                return false;

            var holes = await _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId)
                .ToListAsync();

            var holeParByNumber = holes.ToDictionary(h => h.HoleNumber, h => h.Par);

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId)
                .Include(r => r.Player)
                .Include(r => r.Squad)
                .ToListAsync();

            var resultRows = new List<ResultRow>();

            foreach (var round in rounds)
            {
                var playedScores = await _db.Scores
                    .AsNoTracking()
                    .Where(s => s.RoundId == round.Id && s.Strokes > 0)
                    .OrderBy(s => s.HoleNumber)
                    .ToListAsync();

                int holesPlayed = playedScores.Count;
                int totalStrokes = playedScores.Sum(s => s.Strokes);

                int totalParPlayed = playedScores.Sum(s =>
                    holeParByNumber.TryGetValue(s.HoleNumber, out var par) ? par : 0);

                string playerName = "";

                if (round.Player != null)
                    playerName = $"{round.Player.FirstName} {round.Player.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = $"Joueur #{round.PlayerId}";

                resultRows.Add(new ResultRow
                {
                    RoundId = round.Id,
                    PlayerId = round.PlayerId,
                    PlayerName = playerName,
                    SquadName = round.Squad?.Name ?? "-",
                    HolesPlayed = holesPlayed,
                    TotalStrokes = totalStrokes,
                    TotalParPlayed = totalParPlayed,
                    IsComplete = holesPlayed >= 18
                });
            }

            Results = resultRows
                .OrderBy(r => r.ToPar)
                .ThenByDescending(r => r.HolesPlayed)
                .ThenBy(r => r.TotalStrokes)
                .ThenBy(r => r.PlayerName)
                .ToList();

            for (int i = 0; i < Results.Count; i++)
                Results[i].Rank = i + 1;

            return true;
        }

        public string FormatToPar(int value)
        {
            if (value == 0) return "E";
            if (value > 0) return $"+{value}";
            return value.ToString();
        }
    }
}