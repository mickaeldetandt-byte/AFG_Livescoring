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

        // Nouveaux DbSet pour les doubles
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamPlayer> TeamPlayers => Set<TeamPlayer>();
        public DbSet<TeamRound> TeamRounds => Set<TeamRound>();
        public DbSet<TeamScore> TeamScores => Set<TeamScore>();

        public DbSet<MatchPlayRound> MatchPlayRounds { get; set; }
        public DbSet<MatchPlayHoleResult> MatchPlayHoleResults { get; set; }

        public DbSet<Club> Clubs { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Hole>()
                .HasIndex(h => new { h.CourseId, h.HoleNumber })
                .IsUnique();

            modelBuilder.Entity<Score>()
                .HasIndex(s => new { s.RoundId, s.HoleNumber })
                .IsUnique();

            modelBuilder.Entity<TeamPlayer>()
                .HasIndex(tp => new { tp.TeamId, tp.PlayerId })
                .IsUnique();

            modelBuilder.Entity<TeamPlayer>()
                .HasIndex(tp => new { tp.TeamId, tp.Order })
                .IsUnique();

            modelBuilder.Entity<TeamScore>()
                .HasIndex(ts => new { ts.TeamRoundId, ts.HoleNumber })
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

            modelBuilder.Entity<Score>()
                .HasOne(s => s.Round)
                .WithMany()
                .HasForeignKey(s => s.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            // Team
            modelBuilder.Entity<Team>()
                .HasOne(t => t.Competition)
                .WithMany()
                .HasForeignKey(t => t.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Team>()
                .HasOne(t => t.Squad)
                .WithMany()
                .HasForeignKey(t => t.SquadId)
                .OnDelete(DeleteBehavior.SetNull);

            // TeamPlayer
            modelBuilder.Entity<TeamPlayer>()
                .HasOne(tp => tp.Team)
                .WithMany(t => t.TeamPlayers)
                .HasForeignKey(tp => tp.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeamPlayer>()
                .HasOne(tp => tp.Player)
                .WithMany()
                .HasForeignKey(tp => tp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            // TeamRound
            modelBuilder.Entity<TeamRound>()
                .HasOne(tr => tr.Competition)
                .WithMany()
                .HasForeignKey(tr => tr.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeamRound>()
                .HasOne(tr => tr.Team)
                .WithMany(t => t.TeamRounds)
                .HasForeignKey(tr => tr.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeamRound>()
                .HasOne(tr => tr.Squad)
                .WithMany()
                .HasForeignKey(tr => tr.SquadId)
                .OnDelete(DeleteBehavior.SetNull);

            // TeamScore
            modelBuilder.Entity<TeamScore>()
                .HasOne(ts => ts.TeamRound)
                .WithMany(tr => tr.Scores)
                .HasForeignKey(ts => ts.TeamRoundId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchPlayRound>()
    .HasOne(m => m.Competition)
    .WithMany()
    .HasForeignKey(m => m.CompetitionId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchPlayRound>()
                .HasOne(m => m.Squad)
                .WithMany()
                .HasForeignKey(m => m.SquadId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchPlayRound>()
                .HasOne(m => m.TeamA)
                .WithMany()
                .HasForeignKey(m => m.TeamAId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MatchPlayRound>()
                .HasOne(m => m.TeamB)
                .WithMany()
                .HasForeignKey(m => m.TeamBId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MatchPlayRound>()
                .HasOne(m => m.WinnerTeam)
                .WithMany()
                .HasForeignKey(m => m.WinnerTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MatchPlayHoleResult>()
                .HasOne(h => h.MatchPlayRound)
                .WithMany(m => m.HoleResults)
                .HasForeignKey(h => h.MatchPlayRoundId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchPlayHoleResult>()
                .HasIndex(h => new { h.MatchPlayRoundId, h.HoleNumber })
                .IsUnique();


            modelBuilder.Entity<Competition>()
    .HasOne(c => c.Club)
    .WithMany()
    .HasForeignKey(c => c.ClubId)
    .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Competition>()
                .HasOne(c => c.CreatedByUser)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);


        }
    }
}