using StoreOrchestrator;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddMassTransit( mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskStatusConsumer>();

    mt.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();

public class AskStatusConsumer : IConsumer<Contracts.AskForOrchestratorStatus>
{
    public async Task Consume(ConsumeContext<Contracts.AskForOrchestratorStatus> ctx)
    {
        // compute status...
        await ctx.RespondAsync(new Contracts.SendOrchestratorStatus(ctx.Message.CorrelationId, "RUNNING"));
    }
}