using Auth;
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

            host.Run();
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

                // MassTransit: respond to AskForOrderServiceStatus
                services.AddMassTransit(mt =>
                {
                    mt.SetKebabCaseEndpointNameFormatter();

                    mt.AddConsumer<AskOrderServiceStatusConsumer>();
                    mt.AddConsumer<CreateOrderCourseConsumer>();
                    mt.AddConsumer<CreateOrderUserConsumer>();
                    mt.AddConsumer<ProcessOrderConsumer>();

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

    public sealed class ProcessOrderConsumer : IConsumer<Contracts.ProcessOrder>
    {
        private readonly OrderDbContext _db;

        public ProcessOrderConsumer(OrderDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<Contracts.ProcessOrder> ctx)
        {
            var token = ctx.Message.JwtToken;
            TokenService decoder = new TokenService();
            DecodedToken decoded = decoder.DecodeToken(token);

            if (decoded.Username == null)
            {
                await ctx.RespondAsync(
                    new Contracts.OrderProcessed(ctx.Message.CorrelationId, ctx.Message.AggregateId, Guid.Empty, ctx.Message.CourseIds)
                );
                return;
            }

            //stub for any processing logic
            await Task.Delay(1000);

            await ctx.RespondAsync(
                new Contracts.OrderProcessed(ctx.Message.CorrelationId, ctx.Message.AggregateId, Guid.Parse(decoded.Username), ctx.Message.CourseIds)
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