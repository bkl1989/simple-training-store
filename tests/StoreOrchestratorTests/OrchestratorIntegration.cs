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
        var publisher = _host.Services.GetRequiredService<IPublishEndpoint>();

        var msg = new Contracts.AskForOrchestratorStatus(Guid.NewGuid());
        await publisher.Publish(msg);

        var consumerHarness = 
            _host.Services.GetRequiredService<IConsumerTestHarness<AskStatusConsumer>>();
        //Assert that the consumer of the AskForOrchestratorStatus message
        (await consumerHarness.Consumed.Any<Contracts.AskForOrchestratorStatus>()).Should().BeTrue();

        Task received = await Task.WhenAny(StatusProbe.Instance.taskCompletionSource.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        //Assert that the Store Orchestrator published its status
        StatusProbe.Instance.taskCompletionSource.Task.Status.Should().Be(TaskStatus.RanToCompletion);
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
