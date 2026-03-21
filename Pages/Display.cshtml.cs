using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class DisplayModel : LeaderboardModel
    {
        public DisplayModel(AppDbContext db) : base(db)
        {
        }
    }
}