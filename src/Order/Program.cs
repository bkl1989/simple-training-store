using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Keep your worker (optional)
builder.Services.AddHostedService<Order.Worker>();

// MassTransit: respond to AskForOrderServiceStatus
builder.Services.AddMassTransit(mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskOrderServiceStatusConsumer>();

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

public sealed class AskOrderServiceStatusConsumer : IConsumer<Contracts.AskForOrderServiceStatus>
{
    public Task Consume(ConsumeContext<Contracts.AskForOrderServiceStatus> ctx)
        => ctx.RespondAsync(new Contracts.SendOrderServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
}
