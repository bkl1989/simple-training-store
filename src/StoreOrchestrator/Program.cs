using MassTransit;
using MassTransit.Caching.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace StoreOrchestrator
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var builder = CreateBuilder(args);
            var host = Build(builder);
            host.Run();
        }

        public static async Task SeedDevelopmentDatabase(IHost host)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<StoreOrchestratorUserDbContext>();

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            //await context.Database.EnsureCreatedAsync();
            var anyUsers = await context.StoreOrchestratorUsers.AnyAsync();

            if (!anyUsers)
            {
                //a predictable encryption key (all 0) for development testing
                byte[] key = new byte[32];

                context.StoreOrchestratorUsers.Add(new StoreOrchestratorUser
                {
                    EncryptionKey = key
                });

                await context.SaveChangesAsync();
            }
        }

        public static IHost Build (HostApplicationBuilder builder)
        {
            return builder.Build();   
        }
        //TODO: is this the correct form, rather than IHostBuilder, for similar instances?
        public static HostApplicationBuilder CreateBuilder (string[]args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            builder.Services.AddMassTransit(mt =>
            {
                mt.SetKebabCaseEndpointNameFormatter();

                mt.AddConsumer<AskStoreOrchestratorStatusConsumer>();

                mt.UsingRabbitMq((context, cfg) =>
                {
                    var cfgRoot = context.GetRequiredService<IConfiguration>();
                    var amqp = cfgRoot.GetConnectionString("rabbit");
                    cfg.Host(new Uri(amqp));
                    cfg.ConfigureEndpoints(context);
                });
            });

            return builder;
        }
    }

    public class AskStoreOrchestratorStatusConsumer : IConsumer<Contracts.AskForOrchestratorStatus>
    {
        private readonly StoreOrchestratorUserDbContext _db;

        public AskStoreOrchestratorStatusConsumer(StoreOrchestratorUserDbContext db)
        {
            _db = db;
        }
        public async Task Consume(ConsumeContext<Contracts.AskForOrchestratorStatus> ctx)
        {
            // compute status...
            //TODO: verify the contents
            var user = await _db.StoreOrchestratorUsers.AsNoTracking().FirstOrDefaultAsync();
            await ctx.RespondAsync(new Contracts.SendOrchestratorStatus(ctx.Message.CorrelationId, "RUNNING"));
        }
    }
}
