using Cadence.Core.Interfaces;
using Cadence.Core.Scheduling;
using Cadence.Infrastructure.Notifications;
using Cadence.Infrastructure.Persistence;
using Cadence.Infrastructure.Routines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cadence.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCadenceInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<CadenceDbContext>(sp =>
            {
                var dbPath = Path.Combine(GetCadenceDbDirectory(), "cadence.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                var options = new DbContextOptionsBuilder<CadenceDbContext>()
                    .UseSqlite($"Data Source={dbPath}")
                    .Options;
                return new CadenceDbContext(options);
            });
            services.AddSingleton<ICadenceStore, SqliteCadenceStore>();
            services.AddSingleton<IRoutineSource>(sp =>
            {
                var loader = new JsonRoutineLoader();
                var path = Path.Combine(AppContext.BaseDirectory, "Routines", "default.json");
                var blocks = loader.Load(path);
                return new RoutineClock(blocks);
            });
            services.AddSingleton<INotificationSender, ConsoleNotificationSender>();
            services.AddSingleton<IClock>(sp => new SystemClock());

            return services;
        }
        public  static string GetCadenceDbDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 10; i++)
            {
                if (dir is null) break;
                if (File.Exists(Path.Combine(dir.FullName, "Cadence.sln")))
                return Path.Combine(dir.FullName, "CadenceDB");
                dir = dir.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "CadenceDB");
        }
    }
}