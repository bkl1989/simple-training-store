using Learner;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Learner {
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateBuilder(args).Build();

            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<LearnerUserDbContext>();

            await context.Database.CanConnectAsync();

            if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                await SeedDevelopmentDatabase(host);
            }

            host.Run();
        }
        public static async Task SeedDevelopmentDatabase(IHost host)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<LearnerUserDbContext>();

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await context.Database.EnsureCreatedAsync();
            var anyUsers = await context.LearnerUsers.AnyAsync();

            if (!anyUsers)
            {

                context.LearnerUsers.Add(new LearnerUser
                {
                    FirstName = "John",
                    LastName = "Test"
                });

                await context.SaveChangesAsync();
            }
        }

        public static IHostBuilder CreateBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
            {
                // Register DbContext directly
                services.AddDbContext<LearnerUserDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("LearnerDatabase"),
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure();
                        }
                    )
                );
                // Keep your worker (optional)
                services.AddHostedService<Learner.Worker>();

                // MassTransit: respond to AskForLearnerServiceStatus
                services.AddMassTransit(mt =>
                {
                    mt.SetKebabCaseEndpointNameFormatter();

                    mt.AddConsumer<AskLearnerServiceStatusConsumer>();

                    mt.UsingRabbitMq((context, cfg) =>
                    {
                        var cfgRoot = context.GetRequiredService<IConfiguration>();
                        var amqp = cfgRoot.GetConnectionString("rabbit");
                        cfg.Host(new Uri(amqp));
                        cfg.ConfigureEndpoints(context);
                    });
                });
            });

            return builder;
        }
    }

    public sealed class AskLearnerServiceStatusConsumer : IConsumer<Contracts.AskForLearnerServiceStatus>
    {
        private readonly LearnerUserDbContext _db;
        public AskLearnerServiceStatusConsumer(LearnerUserDbContext db)
        {
            _db = db;
        }
        public async Task Consume(ConsumeContext<Contracts.AskForLearnerServiceStatus> ctx)
        {
            var user = await _db.LearnerUsers.AsNoTracking().FirstOrDefaultAsync();
            await ctx.RespondAsync(new Contracts.SendLearnerServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
        }
    }
}


public sealed class CreateLearnerUserConsumer : IConsumer<Contracts.CreateLearnerUser>
{
    private readonly LearnerUserDbContext _db;

    public CreateLearnerUserConsumer(LearnerUserDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<Contracts.CreateLearnerUser> ctx)
    {
        await _db.LearnerUsers.AddAsync(new LearnerUser
        {
            AggregateId = ctx.Message.aggregateId,
            FirstName = ctx.Message.firstName,
            LastName = ctx.Message.lastName
        });

        await ctx.RespondAsync(
            new Contracts.LearnerUserCreated(ctx.Message.CorrelationId)
        );
    }
}
