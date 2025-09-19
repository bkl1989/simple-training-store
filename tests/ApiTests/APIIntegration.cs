using APIGateway;
using Auth;
using Contracts;
using FluentAssertions;
using Learner;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Order;
using StoreOrchestrator;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
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
        var apiAppBuilder = APIGateway.Program.CreateBuilder(Array.Empty<string>());

        // In-memory SQLite connections (kept open)
        var authConn = new SqliteConnection("DataSource=:memory:;");
        var orchestratorConn = new SqliteConnection("DataSource=:memory:;");
        var orderConn = new SqliteConnection("DataSource=:memory:;");
        var learnerConn = new SqliteConnection("DataSource=:memory:;");
        await authConn.OpenAsync();
        await orchestratorConn.OpenAsync();
        await orderConn.OpenAsync();
        await learnerConn.OpenAsync();

        apiAppBuilder.WebHost.ConfigureServices(services =>
        {
            services.AddDbContext<Auth.AuthUserDbContext>(o => o.UseSqlite(authConn));
            services.AddDbContext<StoreOrchestrator.StoreOrchestratorDbContext>(o => o.UseSqlite(orchestratorConn));
            services.AddDbContext<Order.OrderUserDbContext>(o => o.UseSqlite(orderConn));
            services.AddDbContext<Learner.LearnerDbContext>(o => o.UseSqlite(learnerConn));
        });

        apiAppBuilder.WebHost.UseTestServer();

        apiAppBuilder.Services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<APIGateway.OrchestratorStatusConsumer>();
            
            //create user
            cfg.AddConsumer<CreateUserConsumer>();
            cfg.AddConsumer<CreateCourseConsumer>();
            cfg.AddConsumer<CreateAuthUserConsumer>();
            cfg.AddConsumer<CreateLearnerUserConsumer>();
            cfg.AddConsumer<CreateOrderUserConsumer>();
            //create course
            cfg.AddConsumer<CreateLearnerCourseConsumer>();
            //ask status
            cfg.AddConsumer<AskOrderServiceStatusConsumer>();
            cfg.AddConsumer<AskAuthServiceStatusConsumer>();
            cfg.AddConsumer<AskLearnerServiceStatusConsumer>();
            cfg.AddConsumer<AskStoreOrchestratorStatusConsumer>();

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
            ["ConnectionStrings:AuthDatabase"] = "ignored",
            ["ConnectionStrings:StoreOrchestratorDatabase"] = "ignored",
            ["ConnectionStrings:OrderDatabase"] = "ignored",
            ["ConnectionStrings:LearnerDatabase"] = "ignored",
        });

        apiApp = APIGateway.Program.Build(apiAppBuilder);

        // Ensure schema
        await using (var scope = apiApp.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<Auth.AuthUserDbContext>().Database.EnsureCreatedAsync();
        await using (var scope = apiApp.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<StoreOrchestrator.StoreOrchestratorDbContext>().Database.EnsureCreatedAsync();
        await using (var scope = apiApp.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<Order.OrderUserDbContext>().Database.EnsureCreatedAsync();
        await using (var scope = apiApp.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<Learner.LearnerDbContext>().Database.EnsureCreatedAsync();

        // Seed
        await Auth.Program.SeedDevelopmentDatabase(apiApp);
        await StoreOrchestrator.Program.SeedDevelopmentDatabase(apiApp);
        await Order.Program.SeedDevelopmentDatabase(apiApp);
        await Learner.Program.SeedDevelopmentDatabase(apiApp);

        await apiApp.StartAsync();

        _harness = apiApp.Services.GetRequiredService<ITestHarness>();
        await _harness.Start();

        apiClient = apiApp.GetTestClient();
    }

    [Test]
    public async Task CreatesCourse()
    {
        _harness.Should().NotBeNull();
        var timeout = TimeSpan.FromSeconds(10);

        // Inline listeners (no test-harness helpers needed)
        var bus = apiApp.Services.GetRequiredService<IBus>();

        var learnerTcs = new TaskCompletionSource<ConsumeContext<Contracts.LearnerCourseCreated>>(TaskCreationOptions.RunContinuationsAsynchronously);
        //var learnerTcs = new TaskCompletionSource<ConsumeContext<Contracts.LearnerUserCreated>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var learnerCourseCreatedHandler = bus.ConnectHandler<Contracts.LearnerCourseCreated>(ctx =>
        {
            learnerTcs.TrySetResult(ctx);
            return Task.CompletedTask;
        });
        //var learnerUserCreatedHandler = bus.ConnectHandler<Contracts.LearnerUserCreated>(ctx =>
        //{
        //    learnerTcs.TrySetResult(ctx);
        //    return Task.CompletedTask;
        //});
        //var orderUserCreatedHandler = bus.ConnectHandler<Contracts.OrderUserCreated>(ctx =>
        //{
        //    orderTcs.TrySetResult(ctx);
        //    return Task.CompletedTask;
        //});

        try
        {
            // Arrange + Act
            var courseData = new
            {
                Title = "A great course",
                Description = "You can learn a lot here, yes, I believe so. Yep. If you don't mind forking over 5 grand for some good education.",
                Price = 4999_99 //cents
            };

            var courseDataJson = JsonConvert.SerializeObject(courseData);
            var courseDataContent = new StringContent(courseDataJson, Encoding.UTF8, "application/json");

            var response = await apiClient.PostAsync("/api/v1/courses", courseDataContent);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            var bodyObj = JsonConvert.DeserializeObject<JObject>(body)!;

            var message = bodyObj["message"];
            message.Should().NotBeNull("API should return a 'message' with aggregateId");

            var aggregateIdText = message!["aggregateId"]?.Value<string>();
            aggregateIdText.Should().NotBeNullOrWhiteSpace();
            var aggregateId = Guid.Parse(aggregateIdText!);

            // Await events
            var authEvt = await Wait(learnerTcs.Task, timeout);
            authEvt.Should().NotBeNull();

            //var learnerEvt = await Wait(learnerTcs.Task, timeout);
            //learnerEvt.Should().NotBeNull();

            //var orderEvt = await Wait(orderTcs.Task, timeout);
            //orderEvt.Should().NotBeNull();
        }
        finally
        {
            // Detach handlers so they don't leak to other tests
            learnerCourseCreatedHandler.Disconnect();
            //learnerUserCreatedHandler.Disconnect();
            //orderUserCreatedHandler.Disconnect();
        }
    }

    [Test]
    public async Task CreatesUser()
    {
        _harness.Should().NotBeNull();
        var timeout = TimeSpan.FromSeconds(10);

        // Inline listeners (no test-harness helpers needed)
        var bus = apiApp.Services.GetRequiredService<IBus>();

        var authTcs = new TaskCompletionSource<ConsumeContext<Contracts.AuthUserCreated>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var learnerTcs = new TaskCompletionSource<ConsumeContext<Contracts.LearnerUserCreated>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var orderTcs = new TaskCompletionSource<ConsumeContext<Contracts.OrderUserCreated>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var authUserCreatedHandler = bus.ConnectHandler<Contracts.AuthUserCreated>(ctx =>
        {
            authTcs.TrySetResult(ctx);
            return Task.CompletedTask;
        });
        var learnerUserCreatedHandler = bus.ConnectHandler<Contracts.LearnerUserCreated>(ctx =>
        {
            learnerTcs.TrySetResult(ctx);
            return Task.CompletedTask;
        });
        var orderUserCreatedHandler = bus.ConnectHandler<Contracts.OrderUserCreated>(ctx =>
        {
            orderTcs.TrySetResult(ctx);
            return Task.CompletedTask;
        });

        try
        {
            // Arrange + Act
            var userData = new
            {
                FirstName = "John",
                LastName = "Test",
                EmailAddress = "me@test.com",
                Password = "9r$s0gn#20a!"
            };

            var userDataJson = JsonConvert.SerializeObject(userData);
            var userDataContent = new StringContent(userDataJson, Encoding.UTF8, "application/json");

            var response = await apiClient.PostAsync("/api/v1/users", userDataContent);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            var bodyObj = JsonConvert.DeserializeObject<JObject>(body)!;

            var message = bodyObj["message"];
            message.Should().NotBeNull("API should return a 'message' with aggregateId");

            var aggregateIdText = message!["aggregateId"]?.Value<string>();
            aggregateIdText.Should().NotBeNullOrWhiteSpace();
            var aggregateId = Guid.Parse(aggregateIdText!);

            // Await events
            var authEvt = await Wait(authTcs.Task, timeout);
            authEvt.Should().NotBeNull();

            var learnerEvt = await Wait(learnerTcs.Task, timeout);
            learnerEvt.Should().NotBeNull();

            var orderEvt = await Wait(orderTcs.Task, timeout);
            orderEvt.Should().NotBeNull();
        }
        finally
        {
            // Detach handlers so they don't leak to other tests
            authUserCreatedHandler.Disconnect();
            learnerUserCreatedHandler.Disconnect();
            orderUserCreatedHandler.Disconnect();
        }
    }

    [Test]
    public async Task APIGatewayStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("RUNNING");
    }

    [Test]
    public async Task StoreOrchestratorStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/store-orchestrator-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("RUNNING");
    }

    [Test]
    public async Task OrderServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/order-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("RUNNING");
    }

    [Test]
    public async Task AuthServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/auth-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("RUNNING");
    }

    [Test]
    public async Task LearnerServiceStatusCheck()
    {
        var response = await apiClient.GetAsync("/api/v1/learner-service-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("RUNNING");
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

    private static async Task<T> Wait<T>(Task<T> task, TimeSpan timeout)
    {
        var done = await Task.WhenAny(task, Task.Delay(timeout));
        if (done != task)
            throw new TimeoutException($"Timed out waiting for {typeof(T).Name}");
        return await task;
    }
}
