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

            // Add services common to all environments
            builder.Services.AddOpenApi();

            return builder;
        }

        // 2) (Optional) Default MassTransit wiring, can be omitted in tests
        public static void AddDefaultMassTransit(WebApplicationBuilder builder)
        {
            builder.Services.AddMassTransit(cfg =>
            {
                cfg.AddConsumer<OrchestratorStatusConsumer>();

                cfg.UsingInMemory((context, bus) =>
                {
                    bus.ConfigureEndpoints(context);
                });
            });
        }

        // 3) Build the app (maps middleware/endpoints)
        public static WebApplication Build(WebApplicationBuilder builder)
        {
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.MapGet("/api/v1/status", () => "RUNNING");

            app.MapGet("/api/v1/store-orchestrator-status", async (IRequestClient<Contracts.AskForOrchestratorStatus> client) =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetResponse<Contracts.SendOrchestratorStatus>(
                    new Contracts.AskForOrchestratorStatus(Guid.NewGuid()), cts.Token);
                return response.Message.status;
            });

            return app;
        }

        // 4) Run (or StartAsync/StopAsync in tests)
        public static Task RunAsync(WebApplication app) => app.RunAsync();
    }

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
