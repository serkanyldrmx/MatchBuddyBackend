using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace MatchBuddy.DataAccess
{
    public static class ServiceRegistration
    {
        public static void AddDataAccessRegistration(this IServiceCollection services)
        {
            services.AddScoped<IMatchDal, EFMatchDal>();
            services.AddScoped<IMessageDal, EFMessageDal>();
            services.AddScoped<IPlayerDal, EFPlayerDal>();
            services.AddScoped<IStadiumDal, EFStadiumDal>();
            services.AddScoped<ITeamDal, EFteamDal>();
            services.AddScoped<IPlayerTeamDal, EFPlayerTeamDal>();
            services.AddScoped<IGroupDal, EFGroupDal>();
            services.AddScoped<IGroupMessageDal, EFGroupMessageDal>();
            services.AddScoped<IGroupPlayerDal, EFGroupPLayerDal>();
            services.AddScoped<IMatchTeamDal, EFMatchTeamDal>();
        }
    }
}
