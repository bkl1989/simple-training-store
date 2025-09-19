using k8s.KubeConfigModels;
using MassTransit;
using MassTransit.Caching.Internals;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static MassTransit.MessageHeaders;

namespace StoreOrchestrator
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateBuilder(args).Build();

            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<StoreOrchestratorDbContext>();

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
            var context = scope.ServiceProvider.GetRequiredService<StoreOrchestratorDbContext>();

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await context.Database.EnsureCreatedAsync();
            var anyUsers = await context.StoreOrchestratorUsers.AnyAsync();

            if (!anyUsers)
            {
                //a predictable encryption key (all 0) for development testing
                byte[] key = new byte[32];

                context.StoreOrchestratorUsers.Add(new StoreOrchestratorUser
                {
                    EncryptionKey = key
                });

                await context.SaveChangesAsync();
            }
        }

        //TODO: is this the correct form, rather than IHostBuilder, for similar instances?
        public static IHostBuilder CreateBuilder (string[]args)
        {
            //TODO: using host
            var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
            {
                services.AddHostedService<Worker>();

                // Register DbContext directly
                services.AddDbContext<StoreOrchestratorDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("StoreOrchestratorDatabase"),
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure();
                        }
                    )
                );

                services.AddMassTransit(mt =>
                {
                    mt.SetKebabCaseEndpointNameFormatter();

                    mt.AddConsumer<AskStoreOrchestratorStatusConsumer>();
                    //Create user
                    mt.AddConsumer<CreateUserConsumer>();
                    mt.AddRequestClient<Contracts.CreateAuthUser>();
                    mt.AddRequestClient<Contracts.AuthUserCreated>();
                    mt.AddRequestClient<Contracts.CreateLearnerUser>();
                    mt.AddRequestClient<Contracts.LearnerUserCreated>();
                    mt.AddRequestClient<Contracts.CreateOrderUser>();
                    mt.AddRequestClient<Contracts.OrderUserCreated>();
                    //Create course
                    mt.AddRequestClient<Contracts.CreateLearnerCourse>();
                    mt.AddRequestClient<Contracts.LearnerCourseCreated>();
                    mt.AddRequestClient<Contracts.CreateOrderCourse>();
                    mt.AddRequestClient<Contracts.OrderCourseCreated>();

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

    public class AskStoreOrchestratorStatusConsumer : IConsumer<Contracts.AskForOrchestratorStatus>
    {
        private readonly StoreOrchestratorDbContext _db;

        public AskStoreOrchestratorStatusConsumer(StoreOrchestratorDbContext db)
        {
            _db = db;
        }
        public async Task Consume(ConsumeContext<Contracts.AskForOrchestratorStatus> ctx)
        {
            // compute status...
            //TODO: verify the contents
            var user = await _db.StoreOrchestratorUsers.AsNoTracking().FirstOrDefaultAsync();
            await ctx.RespondAsync(new Contracts.SendOrchestratorStatus(ctx.Message.CorrelationId, "RUNNING"));
        }
    }

    public class CreateUserConsumer : IConsumer<Contracts.CreateUser>
    {
        private readonly StoreOrchestratorDbContext _db;
        private readonly IRequestClient<Contracts.CreateAuthUser> _authUserRequestClient;
        private readonly IRequestClient<Contracts.CreateLearnerUser> _learnerUserRequestClient;
        private readonly IRequestClient<Contracts.CreateOrderUser> _orderUserRequestClient;

        public CreateUserConsumer(
            StoreOrchestratorDbContext db, 
            IRequestClient<Contracts.CreateAuthUser> authUserRequestClient,
            IRequestClient<Contracts.CreateLearnerUser> learnerUserRequestClient,
            IRequestClient<Contracts.CreateOrderUser> orderUserRequestClient
        )
        {
            _db = db;
            _authUserRequestClient = authUserRequestClient;
            _learnerUserRequestClient = learnerUserRequestClient;
            _orderUserRequestClient = orderUserRequestClient;
        }
        public async Task Consume(ConsumeContext<Contracts.CreateUser> ctx)
        {

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await _db.Database.EnsureCreatedAsync();
            var anyUsers = await _db.StoreOrchestratorUsers.AnyAsync();

            var createdSaga = new CreateUserSaga {
                correlationId = Guid.NewGuid(),
                aggregateId = Guid.NewGuid(),
            };

            // TODO: handle sad path
            await _db.CreateUserSagas.AddAsync(createdSaga);

            await ctx.RespondAsync(new Contracts.CreateUserSagaStarted(
                createdSaga.correlationId,
                createdSaga.aggregateId,
                ctx.Message.firstName,
                ctx.Message.lastName,
                ctx.Message.email
            ));

            //TODO: cancellation token
            _authUserRequestClient.GetResponse<Contracts.AuthUserCreated>(
                    new Contracts.CreateAuthUser(
                        createdSaga.correlationId,
                        ctx.Message.email,
                        ctx.Message.password,
                        createdSaga.aggregateId
                    ));

            _learnerUserRequestClient.GetResponse<Contracts.LearnerUserCreated>(
                    new Contracts.CreateLearnerUser(
                        createdSaga.correlationId,
                        ctx.Message.firstName,
                        ctx.Message.lastName,
                        createdSaga.aggregateId
                    ));

            _orderUserRequestClient.GetResponse<Contracts.OrderUserCreated>(
                new Contracts.CreateOrderUser(
                    createdSaga.correlationId,
                    createdSaga.aggregateId
                ));
        }
    }

    public class CreateCourseConsumer : IConsumer<Contracts.CreateCourse>
    {
        private readonly StoreOrchestratorDbContext _db;
        private readonly IRequestClient<Contracts.CreateLearnerCourse> _learnerCourseRequestClient;
        private readonly IRequestClient<Contracts.CreateOrderCourse> _orderCourseRequestClient;

        public CreateCourseConsumer(
            StoreOrchestratorDbContext db,
            IRequestClient<Contracts.CreateLearnerCourse> learnerCourseRequestClient,
            IRequestClient<Contracts.CreateOrderCourse> orderCourseRequestClient
        )
        {
            _db = db;
            _learnerCourseRequestClient = learnerCourseRequestClient;
            _orderCourseRequestClient = orderCourseRequestClient;
        }
        public async Task Consume(ConsumeContext<Contracts.CreateCourse> ctx)
        {

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await _db.Database.EnsureCreatedAsync();
            //var anyUsers = await _db.StoreOrchestratorUsers.AnyAsync();

            var createdSaga = new CreateCourseSaga
            {
                correlationId = Guid.NewGuid(),
                aggregateId = Guid.NewGuid(),
            };

            // TODO: handle sad path
            await _db.CreateCourseSagas.AddAsync(createdSaga);

            await ctx.RespondAsync(new Contracts.CreateCourseSagaStarted(
                createdSaga.correlationId,
                createdSaga.aggregateId,
                ctx.Message.Title,
                ctx.Message.Description,
                ctx.Message.price
            ));

            ////TODO: cancellation token
            _learnerCourseRequestClient.GetResponse<Contracts.LearnerCourseCreated>(
                    new Contracts.CreateLearnerCourse(
                        createdSaga.correlationId,
                        createdSaga.aggregateId,
                        ctx.Message.Title,
                        ctx.Message.Description
                    ));

            _orderCourseRequestClient.GetResponse<Contracts.OrderCourseCreated>(
            new Contracts.CreateOrderCourse(
                createdSaga.correlationId,
                createdSaga.aggregateId,
                ctx.Message.Title,
                ctx.Message.price
            ));

            //_learnerUserRequestClient.GetResponse<Contracts.LearnerUserCreated>(
            //        new Contracts.CreateLearnerUser(
            //            createdSaga.correlationId,
            //            ctx.Message.firstName,
            //            ctx.Message.lastName,
            //            createdSaga.aggregateId
            //        ));

            //_orderUserRequestClient.GetResponse<Contracts.OrderUserCreated>(
            //    new Contracts.CreateOrderUser(
            //        createdSaga.correlationId,
            //        createdSaga.aggregateId
            //    ));
        }
    }
}
