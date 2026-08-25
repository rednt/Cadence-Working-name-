using Cadence.Core.Scheduling;
using Cadence.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cadence.Worker
{
    public sealed class RuleEngineWorker : BackgroundService
{
    private readonly RuleEngine _engine;
    private readonly ILogger<RuleEngineWorker> _logger;
    private readonly ICadenceStore _store;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public RuleEngineWorker(RuleEngine engine, ICadenceStore store, ILogger<RuleEngineWorker> logger)
    {
        _engine = engine;
        _logger = logger;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cadence Rule Engine started. Ticking every {Interval} seconds.", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _engine.TickAsync(stoppingToken);
                await _store.RecordHeartbeatAsync(DateTimeOffset.UtcNow, stoppingToken);
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