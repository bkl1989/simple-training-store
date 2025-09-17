using StoreOrchestrator;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddMassTransit( mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskStoreOrchestratorStatusConsumer>();

    mt.UsingRabbitMq((context, cfg) =>
    {
        var cfgRoot = context.GetRequiredService<IConfiguration>();
        var amqp = cfgRoot.GetConnectionString("rabbit");
        cfg.Host(new Uri(amqp));
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();

public class AskStoreOrchestratorStatusConsumer : IConsumer<Contracts.AskForOrchestratorStatus>
{
    public async Task Consume(ConsumeContext<Contracts.AskForOrchestratorStatus> ctx)
    {
        // compute status...
        await ctx.RespondAsync(new Contracts.SendOrchestratorStatus(ctx.Message.CorrelationId, "RUNNING"));
    }
}