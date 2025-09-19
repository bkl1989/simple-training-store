using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Order {

    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = CreateBuilder(args);
            var host = builder.Build();

            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

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
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await context.Database.EnsureCreatedAsync();
            var anyUsers = await context.OrderUsers.AnyAsync();

            if (!anyUsers)
            {

                context.OrderUsers.Add(new OrderUser
                {
                    AggregateId = Guid.Empty
                });

                await context.SaveChangesAsync();
            }
        }

        public static IHostBuilder CreateBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
            {
                // Register DbContext directly
                services.AddDbContext<OrderDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("OrderDatabase"),
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure();
                        }
                    )
                );
                // Keep your worker (optional)
                services.AddHostedService<Order.Worker>();

                // MassTransit: respond to AskForOrderServiceStatus
                services.AddMassTransit(mt =>
                {
                    mt.SetKebabCaseEndpointNameFormatter();

                    mt.AddConsumer<AskOrderServiceStatusConsumer>();
                    mt.AddConsumer<CreateOrderCourseConsumer>();
                    mt.AddConsumer<CreateOrderUserConsumer>();

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
    public sealed class AskOrderServiceStatusConsumer : IConsumer<Contracts.AskForOrderServiceStatus>
    {
        private readonly OrderDbContext _db;

        public AskOrderServiceStatusConsumer(OrderDbContext db)
        {
            _db = db;
        }
        public async Task Consume(ConsumeContext<Contracts.AskForOrderServiceStatus> ctx)
        {
            var user = await _db.OrderUsers.AsNoTracking().FirstOrDefaultAsync();
            await ctx.RespondAsync(new Contracts.SendOrderServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
        }
    }

    public sealed class CreateOrderUserConsumer : IConsumer<Contracts.CreateOrderUser>
    {
        private readonly OrderDbContext _db;

        public CreateOrderUserConsumer(OrderDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<Contracts.CreateOrderUser> ctx)
        {
            await _db.OrderUsers.AddAsync(new OrderUser
            {
                AggregateId = ctx.Message.aggregateId
            });

            await ctx.RespondAsync(
                new Contracts.OrderUserCreated(ctx.Message.CorrelationId)
            );
        }
    }

    public sealed class CreateOrderCourseConsumer : IConsumer<Contracts.CreateOrderCourse>
    {
        private readonly OrderDbContext _db;

        public CreateOrderCourseConsumer(OrderDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<Contracts.CreateOrderCourse> ctx)
        {
            await _db.OrderCourses.AddAsync(new OrderCourse
            {
                Title = ctx.Message.title,
                Price = ctx.Message.price,
                AggregateId = ctx.Message.AggregateId
            });

            await ctx.RespondAsync(
                new Contracts.OrderCourseCreated(ctx.Message.CorrleationId, ctx.Message.AggregateId)
            );
        }
    }
}