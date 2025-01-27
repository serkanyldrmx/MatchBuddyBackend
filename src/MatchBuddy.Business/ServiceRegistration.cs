using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using Microsoft.Extensions.DependencyInjection;

namespace Business
{
    public static class ServiceRegistration
    {
        public static void AddBussinesRegistration(this IServiceCollection services)
        {
            //services.AddScoped<IMatchCommentsService, MatchManager>();
            services.AddScoped<IMatchService, MatchManager>();
            services.AddScoped<IMessageService, MessageManager>();
            services.AddScoped<IPlayerService, PlayerManager>();
            services.AddScoped<IStadiumService, StadiumManager>();
            services.AddScoped<ITeamService, TeamManager>();
            services.AddScoped<IPlayerTeamService, PlayerTeamManager>();
            services.AddScoped<IGroupService, GroupManager>();
            services.AddScoped<IGroupMessageService, GroupMessageManager>();
            services.AddScoped<IGroupPlayerService, GroupPlayerManager>();
        }
    }
}
