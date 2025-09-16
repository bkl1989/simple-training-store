using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Keep your worker (optional)
builder.Services.AddHostedService<Learner.Worker>();

// MassTransit: respond to AskForLearnerServiceStatus
builder.Services.AddMassTransit(mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskLearnerServiceStatusConsumer>();

    mt.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();

public sealed class AskLearnerServiceStatusConsumer : IConsumer<Contracts.AskForLearnerServiceStatus>
{
    public Task Consume(ConsumeContext<Contracts.AskForLearnerServiceStatus> ctx)
        => ctx.RespondAsync(new Contracts.SendLearnerServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
}
