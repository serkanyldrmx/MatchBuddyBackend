using MatcBuddy.Core.DataAccess;
using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;

namespace MatcBuddy.Core
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
