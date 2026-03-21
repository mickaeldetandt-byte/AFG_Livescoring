using AFG_Livescoring.Models;

namespace AFG_Livescoring.Services
{
    public class MatchPlaySummary
    {
        public int TeamAHolesWon { get; set; }
        public int TeamBHolesWon { get; set; }
        public int HolesPlayed { get; set; }

        public int HolesRemaining => Math.Max(0, 18 - HolesPlayed);

        public int Difference => TeamAHolesWon - TeamBHolesWon;
        public int Lead => Math.Abs(Difference);

        public bool IsAllSquare => Difference == 0;
        public bool TeamAIsLeading => Difference > 0;
        public bool TeamBIsLeading => Difference < 0;

        // Un match est terminé soit au 18, soit s’il est déjà mathematiquement gagné
        public bool IsClinched => Lead > HolesRemaining;
        public bool IsFinished => HolesPlayed >= 18 || IsClinched;

        public bool IsDormie => !IsFinished && Lead > 0 && Lead == HolesRemaining;

        public string StatusText { get; set; } = "AS";
        public string ResultText { get; set; } = "";
        public int? WinnerTeamId { get; set; }
    }

    public static class MatchPlayCalculator
    {
        public static MatchPlaySummary Calculate(MatchPlayRound round, IEnumerable<MatchPlayHoleResult> results)
        {
            var ordered = results
                .OrderBy(x => x.HoleNumber)
                .ToList();

            var summary = new MatchPlaySummary
            {
                HolesPlayed = ordered.Count
            };

            foreach (var hole in ordered)
            {
                if (hole.IsHalved)
                    continue;

                if (hole.WinnerTeamId == round.TeamAId)
                    summary.TeamAHolesWon++;
                else if (hole.WinnerTeamId == round.TeamBId)
                    summary.TeamBHolesWon++;
            }

            string teamAName = round.TeamA?.Name ?? "A";
            string teamBName = round.TeamB?.Name ?? "B";

            if (summary.IsAllSquare)
            {
                summary.StatusText = "AS";
            }
            else if (summary.TeamAIsLeading)
            {
                summary.StatusText = $"{teamAName} +{summary.Lead}";
            }
            else
            {
                summary.StatusText = $"{teamBName} +{summary.Lead}";
            }

            if (summary.IsDormie)
            {
                summary.StatusText += " (Dormie)";
            }

            // Match terminé
            if (summary.IsFinished)
            {
                if (summary.IsAllSquare && summary.HolesPlayed >= 18)
                {
                    summary.ResultText = "AS";
                    summary.WinnerTeamId = null;
                }
                else
                {
                    summary.WinnerTeamId = summary.TeamAIsLeading ? round.TeamAId : round.TeamBId;

                    // Si le match va jusqu'au 18 : 1UP, 2UP, etc.
                    if (summary.HolesPlayed >= 18 && !summary.IsClinched)
                    {
                        summary.ResultText = $"{summary.Lead}UP";
                    }
                    else
                    {
                        // Victoire avant le 18 : 3&2, 4&3, etc.
                        summary.ResultText = $"{summary.Lead}&{summary.HolesRemaining}";
                    }
                }
            }

            return summary;
        }
    }
}