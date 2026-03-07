using Microsoft.EntityFrameworkCore;

namespace AFG_Livescoring.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<Competition> Competitions => Set<Competition>();
        public DbSet<Round> Rounds => Set<Round>();
        public DbSet<Score> Scores => Set<Score>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Hole> Holes => Set<Hole>();
        public DbSet<Squad> Squads => Set<Squad>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Hole>()
                .HasIndex(h => new { h.CourseId, h.HoleNumber })
                .IsUnique();

            modelBuilder.Entity<Course>()
                .HasMany(c => c.Holes)
                .WithOne(h => h.Course)
                .HasForeignKey(h => h.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Competition>()
                .HasOne(c => c.Course)
                .WithMany()
                .HasForeignKey(c => c.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Squad>()
                .HasOne(s => s.Competition)
                .WithMany()
                .HasForeignKey(s => s.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Round>()
                .HasOne(r => r.Competition)
                .WithMany()
                .HasForeignKey(r => r.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Round>()
                .HasOne(r => r.Player)
                .WithMany()
                .HasForeignKey(r => r.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Round>()
                .HasOne(r => r.Squad)
                .WithMany()
                .HasForeignKey(r => r.SquadId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}