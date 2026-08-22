using Cadence.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cadence.Worker
{
    public sealed class RuleEngineWorker : BackgroundService
{
    private readonly RuleEngine _engine;
    private readonly ILogger<RuleEngineWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public RuleEngineWorker(RuleEngine engine, ILogger<RuleEngineWorker> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cadence Rule Engine started. Ticking every {Interval} seconds.", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _engine.TickAsync(stoppingToken);
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