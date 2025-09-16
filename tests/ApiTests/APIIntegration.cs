using APIGateway;
using Auth;
using FluentAssertions;
using Learner;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Order;
using StoreOrchestrator;
using System;
using System.Net;
using System.Threading.Tasks;

namespace ApiTests;

public class IntegrationTests
{
    private WebApplication apiApp = null!;
    private HttpClient apiClient = null!;
    private ITestHarness? _harness;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Build a single TestServer host that includes API + all responders (Option A)
        var apiAppBuilder = Program.CreateBuilder(Array.Empty<string>());

        // IMPORTANT: Use TestServer so GetTestClient() works
        apiAppBuilder.WebHost.UseTestServer();

        // One shared MassTransit test harness (consumers/responders + request clients)
        apiAppBuilder.Services.AddMassTransitTestHarness(cfg =>
        {
            // Keep if needed by other tests
            cfg.AddConsumer<APIGateway.OrchestratorStatusConsumer>();

            // Responders from each microservice (brought into THIS host for the test)
            cfg.AddConsumer<AskStoreOrchestratorStatusConsumer>();
            cfg.AddConsumer<AskOrderServiceStatusConsumer>();
            cfg.AddConsumer<AskAuthServiceStatusConsumer>();
            cfg.AddConsumer<AskLearnerServiceStatusConsumer>();

            // Request clients the API endpoints will resolve
            cfg.AddRequestClient<Contracts.AskForOrchestratorStatus>();
            cfg.AddRequestClient<Contracts.AskForOrderServiceStatus>();
            cfg.AddRequestClient<Contracts.AskForAuthServiceStatus>();
            cfg.AddRequestClient<Contracts.AskForLearnerServiceStatus>();

            cfg.UsingInMemory((context, bus) =>
            {
                bus.ConfigureEndpoints(context);
            });
        });

        apiApp = Program.Build(apiAppBuilder);
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
