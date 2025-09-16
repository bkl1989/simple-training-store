using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace OrchestratorIntegrationTests;

[TestFixture]
public class OrchestratorIntegrationTests
{
    private IHost _host = null!;
    private ITestHarness _harness = null!;

    [SetUp]
    public async Task SetUp()
    {

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<AskStatusConsumer>();

                    cfg.AddConsumer<TestSendStatusConsumer>(cfg =>
                    {
                        cfg.UseConcurrentMessageLimit(1);
                    });

                    cfg.UsingInMemory((context, bus) =>
                    {
                        bus.ConfigureEndpoints(context);
                    });
                });
            })
            .Build();

        _harness = _host.Services.GetRequiredService<ITestHarness>();
        await _host.StartAsync();
        await _harness.Start();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _harness.Stop();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Test]
    public async Task OrchestratorStatusCheck()
    {
        // Resolve a typed request client instead of IPublishEndpoint
        var client = _host.Services.GetRequiredService<
            IRequestClient<Contracts.AskForOrchestratorStatus>>();

        var correlationId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Send request and await the typed response
        var response = await client.GetResponse<Contracts.SendOrchestratorStatus>(
            new Contracts.AskForOrchestratorStatus(correlationId), cts.Token);

        // (Optional) keep your consumer assertion
        var consumerHarness =
            _host.Services.GetRequiredService<IConsumerTestHarness<AskStatusConsumer>>();
        (await consumerHarness.Consumed.Any<Contracts.AskForOrchestratorStatus>())
            .Should().BeTrue("request should be consumed by AskStatusConsumer");

        // Assert on the response payload (this proves the reply arrived)
        response.Message.status.Should().Be("RUNNING");

        // (Optional) Harness-level check: response is **sent**, not published
        var harness = _host.Services.GetRequiredService<ITestHarness>();
        (await harness.Sent.Any<Contracts.SendOrchestratorStatus>())
            .Should().BeTrue("response should be sent back to the requester");
    }
}

public class TestSendStatusConsumer : MassTransit.IConsumer<Contracts.SendOrchestratorStatus>
{
    public StatusProbe probe = StatusProbe.Instance;
    public Task Consume(ConsumeContext<Contracts.SendOrchestratorStatus> ctx)
    {
        probe.taskCompletionSource.TrySetResult(ctx.Message);
        return Task.CompletedTask;
    }

    public TestSendStatusConsumer ()
    {

    }
}

//A singleton so that all testSendStatusConsumer instances complete one task
public class StatusProbe
{
    private static StatusProbe instance = null;
    public static StatusProbe Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new StatusProbe();
            }
            return instance;
        }
    }

    public readonly TaskCompletionSource<Contracts.SendOrchestratorStatus> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
