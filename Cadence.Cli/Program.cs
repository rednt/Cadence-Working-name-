using Cadence.Infrastructure;
using Cadence.Infrastructure.Persistence;
using Cadence.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCadenceInfrastructure();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();
    dbContext.Database.EnsureCreated();
}

await CommandParser.RunAsync(args, host.Services);
