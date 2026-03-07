using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Courses
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db) => _db = db;

        public List<Course> Courses { get; set; } = new();

        [BindProperty]
        public string NewCourseName { get; set; } = string.Empty;

        public void OnGet()
        {
            Courses = _db.Courses.AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();
        }

        public IActionResult OnPostAdd()
        {
            if (string.IsNullOrWhiteSpace(NewCourseName))
            {
                ModelState.AddModelError(string.Empty, "Le nom du parcours est obligatoire.");
                OnGet();
                return Page();
            }

            var course = new Course { Name = NewCourseName.Trim(), IsActive = true };
            _db.Courses.Add(course);
            _db.SaveChanges();

            // ✅ Créer 18 trous par défaut
            for (int i = 1; i <= 18; i++)
            {
                _db.Holes.Add(new Hole
                {
                    CourseId = course.Id,
                    HoleNumber = i,
                    Par = 3,
                    IsActive = true
                });
            }
            _db.SaveChanges();

            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            var course = _db.Courses.FirstOrDefault(c => c.Id == id);
            if (course != null)
            {
                _db.Courses.Remove(course); // cascade => supprime Holes
                _db.SaveChanges();
            }
            return RedirectToPage();
        }
    }
}