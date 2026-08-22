using Cadence.Worker;
using Cadence.Core.Scheduling;
using Cadence.Infrastructure;
using Cadence.Infrastructure.Persistence;


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
host.Run();

