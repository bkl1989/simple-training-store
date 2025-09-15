using System.Net;
using FluentAssertions;
using RestSharp;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiTests;

public class FakeApiTests
{
    [Test]
    public async Task APIGatewayStatusCheck ()
    {
        var url = "/api/v1/status";
        await using var app = new WebApplicationFactory<APIGateway.EntryPointMarker>();
        var client = app.CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }

    [Test]
    public async Task OrchestratorStatusCheck ()
    {
        var url = "/api/v1/store-orchestrator-status";
        await using var app = new WebApplicationFactory<APIGateway.EntryPointMarker>();
        var client = app.CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("RUNNING");
    }
}

