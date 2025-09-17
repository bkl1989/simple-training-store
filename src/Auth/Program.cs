using Auth;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddSqlServerDbContext<UserDBContext>("sqldata");

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

builder.Services.AddDbContext<UserDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("sqldata")));

builder.Build().Run();

var host = builder.Build();

if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<UserDBContext>();
        context.Database.EnsureCreated();
    }
}
else
{

}

host.Run();

public sealed class AskAuthServiceStatusConsumer : IConsumer<Contracts.AskForAuthServiceStatus>
{
    public Task Consume(ConsumeContext<Contracts.AskForAuthServiceStatus> ctx)
        => ctx.RespondAsync(new Contracts.SendAuthServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
}
