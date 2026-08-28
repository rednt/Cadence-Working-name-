using Cadence.Worker;
using Cadence.Core.Scheduling;
using Cadence.Infrastructure;
using Cadence.Infrastructure.Persistence;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCadenceInfrastructure();
builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddHostedService<RuleEngineWorker>();

var host = builder.Build();

using  (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();
    dbContext.Database.EnsureCreated();
}

var pidPath = Path.Combine(AppContext.BaseDirectory, "CadenceDB", "worker.pid");
Directory.CreateDirectory(Path.GetDirectoryName(pidPath)!);
File.WriteAllText(pidPath, Process.GetCurrentProcess().Id.ToString());

try
{
    host.Run();
}
finally
{
    if (File.Exists(pidPath))
        File.Delete(pidPath);
}



