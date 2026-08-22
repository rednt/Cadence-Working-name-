using Cadence.Worker;
using Cadence.Core.Scheduling;
using Cadence.Core.Interfaces;
using Cadence.Infrastructure.Persistence;
using Cadence.Infrastructure.Routines;
using Cadence.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<CadenceDbContext>(sp =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "CadenceDB", "cadence.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    var options = new DbContextOptionsBuilder<CadenceDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;
    return new CadenceDbContext(options);
});

builder.Services.AddSingleton<ICadenceStore, SqliteCadenceStore>();
builder.Services.AddSingleton<IRoutineSource>(sp =>
{
    var loader = new JsonRoutineLoader();
    var path = Path.Combine(AppContext.BaseDirectory, "Routines", "default.json");
    var blocks = loader.Load(path);
    return new RoutineClock(blocks);
});
builder.Services.AddSingleton<INotificationSender, ConsoleNotificationSender>();
builder.Services.AddSingleton<IClock>(sp => new SystemClock());

builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddHostedService<RuleEngineWorker>();

var host = builder.Build();

using  (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();
    dbContext.Database.EnsureCreated();
}
host.Run();

