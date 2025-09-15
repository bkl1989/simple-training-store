using MassTransit;
using MassTransit.Testing;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

IHost massTransitHost = Host.CreateDefaultBuilder()

            .ConfigureServices(services =>
            {
                services.AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<OrchestratorStatusConsumer>();

                    //cfg.AddConsumer<TestSendStatusConsumer>(cfg =>
                    //{
                    //    cfg.UseConcurrentMessageLimit(1);
                    //});

                    cfg.UsingInMemory((context, bus) =>
                    {
                        bus.ConfigureEndpoints(context);
                    });
                });
            })
            .Build();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/api/v1/status", () =>
{
    return "RUNNING";
});

app.MapGet("/api/v1/store-orchestrator-status", async () =>
{
    //Create consumer instance to listen for orchestrator-status events
    OrchestratorStatusConsumer consumer = massTransitHost.Services.GetRequiredService<OrchestratorStatusConsumer>();
    //send an orchestrator status request
    var publisher = massTransitHost.Services.GetRequiredService<IPublishEndpoint>();
    var msg = new Contracts.AskForOrchestratorStatus(Guid.NewGuid());
    publisher.Publish(msg);
    //fulfill the request on the first status message
    //Contracts.SendOrchestratorStatus orchestratorStatusReceived = await consumer.taskCompletionSource.Task;
    //TODO: configurable timeouts
    var waitedForOrchestratorStatus = await Task.WhenAny(consumer.taskCompletionSource.Task, Task.Delay(TimeSpan.FromSeconds(5)));
    if (consumer.taskCompletionSource.Task.IsCompletedSuccessfully)
    {
        Contracts.SendOrchestratorStatus status = await consumer.taskCompletionSource.Task;
        return status.status;
    }
    else
    {
        return "TIMED_OUT";
    }
});

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class OrchestratorStatusConsumer : MassTransit.IConsumer<Contracts.SendOrchestratorStatus>
{

    public readonly TaskCompletionSource<Contracts.SendOrchestratorStatus> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Consume(ConsumeContext<Contracts.SendOrchestratorStatus> ctx)
    {
        return Task.CompletedTask;
    }
}
