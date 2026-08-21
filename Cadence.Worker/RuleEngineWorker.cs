using Cadence.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cadence.Worker
{
    public sealed class RuleEngineWorker : BackgroundService
    {
        // Inject the Scope Factory
        private readonly IServiceScopeFactory _scopeFactory; 
        private readonly ILogger<RuleEngineWorker> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public RuleEngineWorker(IServiceScopeFactory scopeFactory, ILogger<RuleEngineWorker> logger) 
        { 
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cadence Rule Engine started. Ticking every {Interval} seconds.", Interval.TotalSeconds);
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var engine = scope.ServiceProvider.GetRequiredService<RuleEngine>();
                await engine.TickAsync(stoppingToken);
            }
            _logger.LogInformation("Initial tick completed. Entering regular ticking loop.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try 
                { 
                    // Create a fresh scope for every single tick
                    using var scope = _scopeFactory.CreateScope();
                    
                    // Resolve the RuleEngine (and its Scoped DbContext) from the new scope
                    var engine = scope.ServiceProvider.GetRequiredService<RuleEngine>();
                    
                    await engine.TickAsync(stoppingToken); 
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "RuleEngine tick failed"); 
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}