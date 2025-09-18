namespace StoreOrchestrator;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            //Look for sagas that are not in a pending state (sent_at date?)
            //Set the saga to pending and send the event
            //If the sent_at date is stale (randomize), send again
            //TODO: backoff logic

            await Task.Delay(1000, stoppingToken);
        }
    }
}
