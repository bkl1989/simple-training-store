using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Keep your worker (optional)
builder.Services.AddHostedService<Auth.Worker>();

// MassTransit: respond to AskForAuthServiceStatus
builder.Services.AddMassTransit(mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskAuthServiceStatusConsumer>();

    mt.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();

public sealed class AskAuthServiceStatusConsumer : IConsumer<Contracts.AskForAuthServiceStatus>
{
    public Task Consume(ConsumeContext<Contracts.AskForAuthServiceStatus> ctx)
        => ctx.RespondAsync(new Contracts.SendAuthServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
}
