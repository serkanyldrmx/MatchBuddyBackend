using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace MatchBuddy.DataAccess
{
    public class MatchBuddyContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=MatchBuddy;Trusted_Connection=true");
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
        public DbSet<Stadium> Stadiums { get; set; }
        public DbSet<Match> Matchs { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<MatchComment> MatchComments { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<GroupPlayer> GroupPlayers { get; set; }
        public DbSet<MatchTeam> MatchTeam { get; set; }
        public DbSet<PlayerTeam> PlayerTeam { get; set; }

    }
}
