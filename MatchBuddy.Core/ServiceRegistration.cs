using MatchBuddy.Core.DataAccess;
using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;

namespace MatchBuddy.Core
{
    public static class ServiceRegistration
    {
        public static void AddCoreRegistration(this IServiceCollection services)
        {
            //services.AddScoped<IResult, IDataResult>();
            //services.AddScoped<IEntityRepository, EfEntityRepositoryBase>();
            //services.AddScoped<IResult, Result>();
            //services.AddScoped<IDataResult, DataResult>();
        }
    }
}
