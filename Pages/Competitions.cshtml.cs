using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class CompetitionsModel : PageModel
    {
        private readonly AppDbContext _db;

        public CompetitionsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<Competition> Competitions { get; set; } = new();

        // Liste des parcours pour le dropdown
        public List<Course> Courses { get; set; } = new();

        [BindProperty]
        public Competition NewCompetition { get; set; } = new();

        public void OnGet()
        {
            LoadCourses();

            Competitions = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.Name)
                .ToList();

            // Defaults du formulaire (GET uniquement)
            NewCompetition.Date = DateTime.Today;
            NewCompetition.ScoringMode = ScoringMode.SquadOnly;
        }

        private void LoadCourses()
        {
            Courses = _db.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
        }

        public IActionResult OnPostAdd()
        {
            LoadCourses();

            if (string.IsNullOrWhiteSpace(NewCompetition.Name))
            {
                ModelState.AddModelError(string.Empty, "Le nom de la compétition est obligatoire.");
            }

            // Parcours obligatoire
            if (NewCompetition.CourseId == null || !_db.Courses.Any(c => c.Id == NewCompetition.CourseId))
            {
                ModelState.AddModelError(string.Empty, "Veuillez sélectionner un parcours.");
            }

            // Mode valide
            if (!Enum.IsDefined(typeof(ScoringMode), NewCompetition.ScoringMode))
            {
                ModelState.AddModelError(string.Empty, "Mode invalide.");
            }

            if (!ModelState.IsValid)
            {
                Competitions = _db.Competitions
                    .Include(c => c.Course)
                    .AsNoTracking()
                    .OrderByDescending(c => c.Date)
                    .ThenBy(c => c.Name)
                    .ToList();

                return Page();
            }

            _db.Competitions.Add(NewCompetition);
            _db.SaveChanges();

            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            var comp = _db.Competitions.FirstOrDefault(c => c.Id == id);
            if (comp == null)
            {
                return RedirectToPage();
            }

            // 1) Récupérer tous les rounds liés à la compétition
            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == id)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            // 2) Supprimer tous les scores liés à ces rounds
            if (roundIds.Any())
            {
                var scores = _db.Scores
                    .Where(s => roundIds.Contains(s.RoundId))
                    .ToList();

                if (scores.Any())
                {
                    _db.Scores.RemoveRange(scores);
                    _db.SaveChanges();
                }
            }

            // 3) Détacher les rounds des squads
            if (rounds.Any())
            {
                foreach (var round in rounds)
                {
                    round.SquadId = null;
                }

                _db.SaveChanges();
            }

            // 4) Supprimer les rounds
            if (rounds.Any())
            {
                _db.Rounds.RemoveRange(rounds);
                _db.SaveChanges();
            }

            // 5) Supprimer les squads liées à la compétition
            var squads = _db.Squads
                .Where(s => s.CompetitionId == id)
                .ToList();

            if (squads.Any())
            {
                _db.Squads.RemoveRange(squads);
                _db.SaveChanges();
            }

            // 6) Supprimer la compétition
            _db.Competitions.Remove(comp);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Compétition supprimée avec succès.";
            return RedirectToPage();
        }
    }
}