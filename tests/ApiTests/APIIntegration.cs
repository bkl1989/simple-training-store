using APIGateway;
using Auth;
using FluentAssertions;
using k8s.KubeConfigModels;
using Learner;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Order;
using StoreOrchestrator;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Linq; // <-- added

namespace ApiTests;

public class IntegrationTests
{
    private WebApplication apiApp = null!;
    private HttpClient apiClient = null!;
    private ITestHarness? _harness;

    private async void setUpDatabaseForContext()
    {

    }

    private async void setUpDatabaseForService()
    {

    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Build a single TestServer host that includes API + all responders (Option A)
        var apiAppBuilder = APIGateway.Program.CreateBuilder(Array.Empty<string>());

        // Keep one open connection for the lifetime of the test host
        var conn = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        await conn.OpenAsync();

        // Override the DbContext for tests

        apiAppBuilder.WebHost.ConfigureServices(services =>
        {
            // Add SQLite DbContext using the open connection
            services.AddDbContext<Auth.AuthUserDbContext>( options => options.UseSqlite(conn));
            services.AddDbContext<StoreOrchestrator.StoreOrchestratorUserDbContext>(options => options.UseSqlite(conn));
        });

        // IMPORTANT: Use TestServer so GetTestClient() works
        apiAppBuilder.WebHost.UseTestServer();

        // One shared MassTransit test harness (consumers/responders + request clients)
        apiAppBuilder.Services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<APIGateway.OrchestratorStatusConsumer>();
            cfg.AddConsumer<AskStoreOrchestratorStatusConsumer>();
            cfg.AddConsumer<AskOrderServiceStatusConsumer>();
            cfg.AddConsumer<AskAuthServiceStatusConsumer>();
            cfg.AddConsumer<AskLearnerServiceStatusConsumer>();

            cfg.AddRequestClient<Contracts.AskForOrchestratorStatus>();
            cfg.AddRequestClient<Contracts.AskForOrderServiceStatus>();
            cfg.AddRequestClient<Contracts.AskForAuthServiceStatus>();
            cfg.AddRequestClient<Contracts.AskForLearnerServiceStatus>();

            cfg.UsingInMemory((context, bus) =>
            {
                bus.ConfigureEndpoints(context);
            });
        });

        apiAppBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sqldata"] = "Server=ignored;Database=ignored;"
        });

        apiApp = APIGateway.Program.Build(apiAppBuilder);

        // Ensure schema using the SAME container the app uses
        await using (var scope = apiApp.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Auth.AuthUserDbContext>();
            await db.Database.MigrateAsync();
        }

        await using (var scope = apiApp.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreOrchestrator.StoreOrchestratorUserDbContext>();
            await db.Database.MigrateAsync();
        }

        // Your dev seed now resolves UserDBContext from the app container
        await Auth.Program.SeedDevelopmentDatabase(apiApp);
        await StoreOrchestrator.Program.SeedDevelopmentDatabase(apiApp);

        await apiApp.StartAsync();

        _harness = apiApp.Services.GetRequiredService<ITestHarness>();
        await _harness.Start();

        apiClient = apiApp.GetTestClient();
    }

    [Test]
    public async Task APIGatewayStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [Test]
    public async Task StoreOrchestratorStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/store-orchestrator-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [Test]
    public async Task OrderServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/order-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [Test]
    public async Task AuthServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/auth-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [Test]
    public async Task LearnerServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/learner-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_harness is not null)
            await _harness.Stop();

        apiClient?.Dispose();

        if (apiApp is not null)
        {
            await apiApp.StopAsync();
            await apiApp.DisposeAsync();
        }
    }
}
