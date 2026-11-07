using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TodoAPI.Application.Services;

public sealed class RecurrenceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurrenceWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessDueAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessDueAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
        }
    }

    public async Task<int> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recurrenceService = scope.ServiceProvider.GetRequiredService<RecurrenceService>();
            return await recurrenceService.ProcessDueAsync(nowUtc, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao processar ocorrências recorrentes vencidas.");
            return 0;
        }
    }
}
