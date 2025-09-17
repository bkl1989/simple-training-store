using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace APIGateway
{
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
