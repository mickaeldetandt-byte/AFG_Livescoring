using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Courses
{
    public class HolesModel : PageModel
    {
        private readonly AppDbContext _db;
        public HolesModel(AppDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)]
        public int CourseId { get; set; }

        public Course? Course { get; set; }

        [BindProperty]
        public List<int> Pars { get; set; } = new();

        public IActionResult OnGet()
        {
            Course = _db.Courses.AsNoTracking().FirstOrDefault(c => c.Id == CourseId);
            if (Course == null) return RedirectToPage("/Courses/Index");

            var holes = _db.Holes.AsNoTracking()
                .Where(h => h.CourseId == CourseId)
                .OrderBy(h => h.HoleNumber)
                .ToList();

            Pars = new List<int>();
            for (int i = 1; i <= 18; i++)
            {
                var h = holes.FirstOrDefault(x => x.HoleNumber == i);
                Pars.Add(h?.Par ?? 3);
            }

            return Page();
        }

        public IActionResult OnPostSave()
        {
            for (int i = 0; i < 18; i++)
            {
                int holeNumber = i + 1;
                int par = Pars[i];

                if (par < 2) par = 2;
                if (par > 6) par = 6;

                var hole = _db.Holes.FirstOrDefault(h => h.CourseId == CourseId && h.HoleNumber == holeNumber);
                if (hole == null)
                {
                    _db.Holes.Add(new Hole { CourseId = CourseId, HoleNumber = holeNumber, Par = par, IsActive = true });
                }
                else
                {
                    hole.Par = par;
                }
            }

            _db.SaveChanges();
            return RedirectToPage(new { courseId = CourseId });
        }
    }
}