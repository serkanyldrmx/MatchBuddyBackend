using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace MatcBuddy.DataAccess
{
    public static class ServiceRegistration
    {
        public static void AddDataAccessRegistration(this IServiceCollection services)
        {
            services.AddScoped<IMatchDal, EFMatchDal>();
            services.AddScoped<MatcBuddy.DataAccess.Concrete.EntityFramework.IReliabilityDal, MatcBuddy.DataAccess.Concrete.EntityFramework.EFReliabilityDal>();
            services.AddScoped<IMessageDal, EFMessageDal>();
            services.AddScoped<IPlayerDal, EFPlayerDal>();
            services.AddScoped<IStadiumDal, EFStadiumDal>();
            services.AddScoped<ITeamDal, EFteamDal>();
            services.AddScoped<IPlayerTeamDal, EFPlayerTeamDal>();
            services.AddScoped<IGroupDal, EFGroupDal>();
            services.AddScoped<IGroupMessageDal, EFGroupMessageDal>();
            services.AddScoped<IGroupPlayerDal, EFGroupPLayerDal>();
            services.AddScoped<IMatchTeamDal, EFMatchTeamDal>();
            services.AddScoped<IMatchCommentsDal, EFMatchCommentsDal>();
            services.AddScoped<INotificationDal, EFNotificationDal>();
            services.AddScoped<IPlayerNotificationDal, EFPlayerNotificationDal>();
            services.AddScoped<IMatchParticipationDal, EFMatchParticipationDal>();
            services.AddScoped<ISocialDal, EFSocialDal>();
            services.AddScoped<IInviteDal, EFInviteDal>();
            services.AddScoped<ITournamentDal, EFTournamentDal>();
            services.AddScoped<IVenueDal, EFVenueDal>();
            services.AddScoped<IChatDal, EFChatDal>();
            services.AddScoped<IAuthDal, EFAuthDal>();
            services.AddScoped<IExternalAccountStore>(sp => sp.GetRequiredService<IAuthDal>());
        }
    }
}
