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
            var context = scope.ServiceProvider.GetRequiredService<LearnerDbContext>();

            await context.Database.CanConnectAsync();

            host.Run();
        }

        public static IHostBuilder CreateBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
            {
                // Register DbContext directly
                services.AddDbContext<LearnerDbContext>(options =>
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
        private readonly LearnerDbContext _db;
        public AskLearnerServiceStatusConsumer(LearnerDbContext db)
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
    private readonly LearnerDbContext _db;

    public CreateLearnerUserConsumer(LearnerDbContext db)
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

public sealed class CreateLearnerCourseConsumer : IConsumer<Contracts.CreateLearnerCourse>
{
    private readonly LearnerDbContext _db;

    public CreateLearnerCourseConsumer(LearnerDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<Contracts.CreateLearnerCourse> ctx)
    {
        await _db.LearnerCourses.AddAsync(new LearnerCourse
        {
            AggregateId = ctx.Message.AggregateId,
            Title = ctx.Message.Title,
            Description = ctx.Message.Description
        });

        await ctx.RespondAsync(
            new Contracts.LearnerCourseCreated(ctx.Message.CorrelationId, ctx.Message.AggregateId)
        );
    }
}
