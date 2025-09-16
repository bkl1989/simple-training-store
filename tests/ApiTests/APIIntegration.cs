using APIGateway;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StoreOrchestrator; // for AskStatusConsumer if it's defined there
using System.Net;
using System.Threading.Tasks;

namespace ApiTests;

public class IntegrationTests
{
    private HttpClient apiClient = null!;
    private WebApplication apiApp = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Create the app builder and enable TestServer
        var apiAppBuilder = Program.CreateBuilder([]);

        // IMPORTANT: Use TestServer so GetTestClient() works
        apiAppBuilder.WebHost.UseTestServer();

        // Add one shared MassTransit test harness (consumers from both services)
        apiAppBuilder.Services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrchestratorStatusConsumer>();
            cfg.AddConsumer<AskStatusConsumer>(); // from StoreOrchestrator project

            cfg.UsingInMemory((context, bus) =>
            {
                bus.ConfigureEndpoints(context);
            });
        });

        // Build the app via your Program.Build (maps endpoints, middleware, etc.)
        apiApp = Program.Build(apiAppBuilder);

        await apiApp.StartAsync();

        // In-proc HttpClient to TestServer
        apiClient = apiApp.GetTestClient();

        // Optional: start the harness explicitly (host start usually does this)
        // var harness = apiApp.Services.GetRequiredService<ITestHarness>();
        // await harness.Start();
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

    [OneTimeTearDown]
    public async Task TearDown()
    {
        apiClient.Dispose();

        await apiApp.StopAsync();
        await apiApp.DisposeAsync();
    }
}
