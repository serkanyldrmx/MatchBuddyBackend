using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace MatchBuddy.DataAccess
{
    public class MatchBuddyContext : DbContext
    {
        // Uygulama açılışında appsettings'teki "ConnectionStrings:MatchBuddy" ile set edilir.
        // Varsayılan değer yalnızca design-time (dotnet ef migrations) için kullanılır.
        public static string ConnectionString { get; set; } =
            @"Server=(localdb)\mssqllocaldb;Database=MatchBuddy;Trusted_Connection=true";

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(ConnectionString);
            }
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
        public DbSet<Notification> Notification { get; set; }
        public DbSet<PlayerNotification> PlayerNotification { get; set; }
        public DbSet<MatchParticipant> MatchParticipants { get; set; }
        public DbSet<MatchGuest> MatchGuests { get; set; }
        public DbSet<MatchLike> MatchLikes { get; set; }
        public DbSet<MvpVote> MvpVotes { get; set; }
        public DbSet<PlayerRating> PlayerRatings { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<PlayerSettings> PlayerSettings { get; set; }
        public DbSet<MatchInvite> MatchInvites { get; set; }
        public DbSet<TeamInvite> TeamInvites { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentTeam> TournamentTeams { get; set; }
        public DbSet<TournamentFixture> TournamentFixtures { get; set; }
        public DbSet<StadiumPhoto> StadiumPhotos { get; set; }
        public DbSet<Pitch> Pitches { get; set; }
        public DbSet<OpeningHour> OpeningHours { get; set; }
        public DbSet<PriceRule> PriceRules { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<SlotBlock> SlotBlocks { get; set; }
        public DbSet<VenueReview> VenueReviews { get; set; }
        public DbSet<VenueExpense> VenueExpenses { get; set; }
        public DbSet<Media> Media { get; set; }
        public DbSet<SystemParameter> SystemParameters { get; set; }
        public DbSet<ParameterAudit> ParameterAudits { get; set; }
        public DbSet<VenueSettings> VenueSettings { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ExternalLogin> ExternalLogins { get; set; }
        public DbSet<AuthCode> AuthCodes { get; set; }
        public DbSet<ReliabilityEvent> ReliabilityEvents { get; set; }

    }
}
