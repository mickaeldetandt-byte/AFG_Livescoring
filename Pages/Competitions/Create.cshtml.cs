using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AFG_Livescoring.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        public CreateModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<Course> Courses { get; private set; } = new();

        public sealed class InputModel
        {
            [Required(ErrorMessage = "Le nom de la compétition est obligatoire.")]
            public string Name { get; set; } = string.Empty;

            [Required]
            public DateTime Date { get; set; } = DateTime.Today;

            [Required(ErrorMessage = "Veuillez sélectionner un parcours.")]
            public int? CourseId { get; set; }

            [Required]
            public string Mode { get; set; } = "Competition";

            public CompetitionType CompetitionType { get; set; } =
                CompetitionType.IndividualStrokePlay;

            public CompetitionVisibility Visibility { get; set; } =
                CompetitionVisibility.Public;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUser = await GetCurrentUserAsync();
            if (!CanCreateCompetition(currentUser))
                return Forbid();

            await LoadCoursesAsync();
            Input.Date = DateTime.Today;
            Input.Mode = "Competition";
            Input.CompetitionType = CompetitionType.IndividualStrokePlay;
            Input.Visibility = CompetitionVisibility.Public;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await GetCurrentUserAsync();
            if (!CanCreateCompetition(currentUser))
                return Forbid();

            if (string.IsNullOrWhiteSpace(Input.Name))
            {
                ModelState.AddModelError(
                    "Input.Name",
                    "Le nom de la compétition est obligatoire.");
            }

            if (!string.Equals(Input.Mode, "Competition", StringComparison.Ordinal)
                && !string.Equals(Input.Mode, "Training", StringComparison.Ordinal))
            {
                ModelState.AddModelError("Input.Mode", "Mode invalide.");
            }

            if (!Enum.IsDefined(typeof(CompetitionType), Input.CompetitionType))
            {
                ModelState.AddModelError(
                    "Input.CompetitionType",
                    "Format de jeu invalide.");
            }

            if (!Enum.IsDefined(typeof(CompetitionVisibility), Input.Visibility))
            {
                ModelState.AddModelError(
                    "Input.Visibility",
                    "Visibilité invalide.");
            }

            var courseIsActive = Input.CourseId.HasValue
                                 && await _db.Courses
                                     .AsNoTracking()
                                     .AnyAsync(
                                         course => course.Id == Input.CourseId.Value
                                                   && course.IsActive,
                                         HttpContext.RequestAborted);

            if (!courseIsActive)
            {
                ModelState.AddModelError(
                    "Input.CourseId",
                    "Le parcours sélectionné est introuvable ou inactif.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCoursesAsync();
                return Page();
            }

            var isTraining = string.Equals(
                Input.Mode,
                "Training",
                StringComparison.Ordinal);

            var competition = new Competition
            {
                Name = Input.Name.Trim(),
                Date = Input.Date,
                CourseId = Input.CourseId,
                Mode = Input.Mode,
                CompetitionType = Input.CompetitionType,
                Visibility = Input.Visibility,
                ScoringMode = isTraining
                    ? ScoringMode.IndividualAllowed
                    : ScoringMode.SquadOnly,
                Status = CompetitionStatus.Draft,
                IsActive = true,
                CreatedByUserId = currentUser!.Id,
                ClubId = string.Equals(
                    currentUser.Role,
                    "Club",
                    StringComparison.OrdinalIgnoreCase)
                    ? currentUser.ClubId
                    : null
            };

            _db.Competitions.Add(competition);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            return RedirectToPage("/Competitions");
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            if (User.Identity?.IsAuthenticated != true
                || !int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var userId))
            {
                return null;
            }

            return await _db.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.Id == userId && user.IsActive,
                    HttpContext.RequestAborted);
        }

        private static bool CanCreateCompetition(AppUser? currentUser)
        {
            if (currentUser == null)
                return false;

            if (string.Equals(
                    currentUser.Role,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(
                       currentUser.Role,
                       "Club",
                       StringComparison.OrdinalIgnoreCase)
                   && currentUser.ClubId.HasValue;
        }

        private async Task LoadCoursesAsync()
        {
            Courses = await _db.Courses
                .AsNoTracking()
                .Where(course => course.IsActive)
                .OrderBy(course => course.Name)
                .ToListAsync(HttpContext.RequestAborted);
        }
    }
}
