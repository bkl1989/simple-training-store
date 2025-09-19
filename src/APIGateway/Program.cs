using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Auth;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace APIGateway
{
    public class CreateUserDTO
    {
        [Required] public string FirstName { get; set; }
        [Required] public string LastName { get; set; }
        [Required] public string EmailAddress { get; set; }

        [Required] public string Password { get; set; }
    }

    public class CreateCourseDTO
    {
        [Required] public string Title { get; set; }
        [Required] public string Description { get; set; }
        [Required] public int Price { get; set; }
    }

    public class CredentialsDTO
    {
        [Required] public string Username { get; set; }

        [Required] public string Password { get; set; }
    }

    public class OrderDTO
    {
        [Required] public Guid[] CourseIds { get; set; }
    }

    public class Program
    {
        // ---- Entry point for production ----
        public static async Task Main(string[] args)
        {
            var builder = CreateBuilder(args);

            // Default (prod) MassTransit wiring; tests can skip/replace this.
            AddDefaultMassTransit(builder);

            var app = Build(builder);
            await RunAsync(app);
        }

        // ---- Test-friendly surface area ----

        // 1) Create the builder (tests can call this, add/replace services, then Build)
        public static WebApplicationBuilder CreateBuilder(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddOpenApi(); // OpenAPI for dev if you want it
            return builder;
        }

        // 2) Default MassTransit wiring (register request clients for all services)
        public static void AddDefaultMassTransit(WebApplicationBuilder builder)
        {
            builder.Services.AddMassTransit(cfg =>
            {
                // Kept for compatibility with any test harness code that references it
                cfg.AddConsumer<OrchestratorStatusConsumer>();

                // Request clients for R/R
                cfg.AddRequestClient<Contracts.AskForOrchestratorStatus>();
                cfg.AddRequestClient<Contracts.AskForOrderServiceStatus>();
                cfg.AddRequestClient<Contracts.AskForAuthServiceStatus>();
                cfg.AddRequestClient<Contracts.AskForLearnerServiceStatus>();
                //create user
                cfg.AddRequestClient<Contracts.CreateUser>();
                cfg.AddRequestClient<Contracts.CreateUserSagaStarted>();
                //create course
                cfg.AddRequestClient<Contracts.CreateCourse>();
                cfg.AddRequestClient<Contracts.CreateCourseSagaStarted>();
                //authenticate
                cfg.AddRequestClient<Contracts.ValidateCredentials>();
                cfg.AddRequestClient<Contracts.CredentialsWereValidated>();

                cfg.UsingRabbitMq((context, cfg) =>
                {
                    var cfgRoot = context.GetRequiredService<IConfiguration>();
                    var amqp = cfgRoot.GetConnectionString("rabbit");
                    cfg.Host(new Uri(amqp));
                    cfg.ConfigureEndpoints(context);
                });
            });
        }

        // 3) Build the app (map endpoints)
        public static WebApplication Build(WebApplicationBuilder builder)
        {
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            // Simple liveness
            app.MapGet("/api/v1/status", () => "RUNNING");

            // Orchestrator status via request/response
            app.MapGet("/api/v1/store-orchestrator-status",
                async (IRequestClient<Contracts.AskForOrchestratorStatus> client) =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var response = await client.GetResponse<Contracts.SendOrchestratorStatus>(
                        new Contracts.AskForOrchestratorStatus(Guid.NewGuid()), cts.Token);
                    return response.Message.status;
                });

            // Order service status via request/response
            app.MapGet("/api/v1/order-service-status",
                async (IRequestClient<Contracts.AskForOrderServiceStatus> client) =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var response = await client.GetResponse<Contracts.SendOrderServiceStatus>(
                        new Contracts.AskForOrderServiceStatus(Guid.NewGuid()), cts.Token);
                    return response.Message.status;
                });

            // Auth service status via request/response
            app.MapGet("/api/v1/auth-service-status",
                async (IRequestClient<Contracts.AskForAuthServiceStatus> client) =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var response = await client.GetResponse<Contracts.SendAuthServiceStatus>(
                        new Contracts.AskForAuthServiceStatus(Guid.NewGuid()), cts.Token);
                    return response.Message.status;
                });

            // Learner service status via request/response
            app.MapGet("/api/v1/learner-service-status",
                async (IRequestClient<Contracts.AskForLearnerServiceStatus> client) =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var response = await client.GetResponse<Contracts.SendLearnerServiceStatus>(
                        new Contracts.AskForLearnerServiceStatus(Guid.NewGuid()), cts.Token);
                    return response.Message.status;
                });

            app.MapPost("/api/v1/users",
            async (CreateUserDTO user, IRequestClient<Contracts.CreateUser> client, CancellationToken ct) =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetResponse<Contracts.CreateUserSagaStarted>(
                    new Contracts.CreateUser(
                        Guid.NewGuid(),
                        user.FirstName,
                        user.LastName,
                        user.EmailAddress,
                        user.Password
                    ), cts.Token);

                return Results.Ok(response);
            });

            app.MapPost("/api/v1/courses",
            async (CreateCourseDTO course, IRequestClient<Contracts.CreateCourse> client, CancellationToken ct) =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetResponse<Contracts.CreateCourseSagaStarted>(
                    new Contracts.CreateCourse(
                        Guid.NewGuid(),
                        course.Title,
                        course.Description,
                        course.Price
                    ), cts.Token);

                return Results.Ok(response);
            });

            app.MapPost("/api/v1/auth",
            async (CredentialsDTO credentials, IRequestClient<Contracts.ValidateCredentials> client, CancellationToken ct) =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetResponse<Contracts.CredentialsWereValidated>(
                    new Contracts.ValidateCredentials(
                        Guid.NewGuid(),
                        credentials.Username,
                        credentials.Password
                    ), cts.Token);

                return Results.Ok(response);
            });

            app.MapPost("/api/v1/orders",
            async (OrderDTO order, IRequestClient<Contracts.CreateOrder> client, CancellationToken ct) =>
            {
                string JWTToken = "";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetResponse<Contracts.CreateOrderSagaStarted>(
                    new Contracts.CreateOrder(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        JWTToken,
                        order.CourseIds
                    ), cts.Token);
            });

            return app;
        }

        // 4) Run (or StartAsync/StopAsync in tests)
        public static Task RunAsync(WebApplication app) => app.RunAsync();
    }

    // Kept for compatibility with any test harness code that references it
    public class OrchestratorStatusConsumer : IConsumer<Contracts.SendOrchestratorStatus>
    {
        public readonly TaskCompletionSource<Contracts.SendOrchestratorStatus> TaskCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Consume(ConsumeContext<Contracts.SendOrchestratorStatus> ctx)
        {
            TaskCompletionSource.TrySetResult(ctx.Message);
            return Task.CompletedTask;
        }
    }
}
