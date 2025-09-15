using System.Net;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
//using MassTransit.Transports;
using Microsoft.AspNetCore.Mvc.Testing;
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

                    cfg.AddConsumer<TestSendStatusConsumer>();

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
        var publisher = _host.Services.GetRequiredService<IPublishEndpoint>();

        var msg = new Contracts.AskForOrchestratorStatus(Guid.NewGuid());
        await publisher.Publish(msg);

        var consumerHarness = 
            _host.Services.GetRequiredService<IConsumerTestHarness<AskStatusConsumer>>();
        //Assert that the consumer of the AskForOrchestratorStatus message
        (await consumerHarness.Consumed.Any<Contracts.AskForOrchestratorStatus>()).Should().BeTrue();

        //var probe = _host.Services.GetRequiredService<TestSendStatusConsumer>();

        //var received = await Task.WhenAny(probe.Seen.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        ////Assert that the Store Orchestrator published its status
        //received.Should().Be(probe.Seen.Task, "Sometime soon after we publish ask for orchestrator status, we should see a sendorchestrator status message");
    }
}

public class TestSendStatusConsumer : MassTransit.IConsumer<Contracts.SendOrchestratorStatus>
{
    public TaskCompletionSource<Contracts.SendOrchestratorStatus> Seen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Consume(ConsumeContext<Contracts.SendOrchestratorStatus> ctx)
    {
        Seen.TrySetResult(ctx.Message);
        return Task.CompletedTask;
    }
}

