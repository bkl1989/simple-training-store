using Auth;
using Contracts;
using k8s.KubeConfigModels;
using MassTransit;
using MessagePack.Formatters;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Security.Cryptography;
using static System.Net.WebRequestMethods;
using Microsoft.Extensions.Configuration;

namespace Auth
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            // Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            await context.Database.CanConnectAsync();
            //TODO: handle error

            if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                await SeedDevelopmentDatabase(host);
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    var tokenService = new TokenService();
                    services.AddSingleton<ITokenService>(tokenService);
                    // Register DbContext directly
                    services.AddDbContext<AuthDbContext>(options =>
                        options.UseSqlServer(context.Configuration.GetConnectionString("AuthDatabase"),
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                sqlOptions.EnableRetryOnFailure();
                            }
                        )
                    );

                    // Optional worker
                    services.AddHostedService<Auth.Worker>();

                    // MassTransit configuration
                    services.AddMassTransit(mt =>
                    {
                        mt.SetKebabCaseEndpointNameFormatter();
                        mt.AddConsumer<AskAuthServiceStatusConsumer>();

                        mt.UsingRabbitMq((ctx, cfg) =>
                        {
                            var cfgRoot = ctx.GetRequiredService<IConfiguration>();
                            var amqp = cfgRoot.GetConnectionString("rabbit");
                            cfg.Host(new Uri(amqp));
                            cfg.ConfigureEndpoints(ctx);
                        });
                    });
                });

        public static async Task SeedDevelopmentDatabase(IHost host)
        {
            //Seeding the development database in this way is deprecated and will be removed

            //await using var scope = host.Services.CreateAsyncScope();
            //var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            //// Ensure schema exists. Use MigrateAsync() if you rely on EF migrations.
            //await context.Database.EnsureCreatedAsync();

            //if (!await context.AuthUsers.AnyAsync())
            //{
            //    const string password = "123Password";

            //    // Generate a salt
            //    byte[] salt = RandomNumberGenerator.GetBytes(16);

            //    // Derive a key from the password + salt using PBKDF2
            //    using var pbkdf2 = new Rfc2898DeriveBytes(
            //        password,
            //        salt,
            //        iterations: 100_000,
            //        HashAlgorithmName.SHA256);

            //    byte[] hash = pbkdf2.GetBytes(32);

            //    Guid aggregateId = Guid.Empty;

            //    context.AuthUsers.Add(new AuthUser
            //    {
            //        EmailAddress = "bkl1989@gmail.com",
            //        HashedPassword = hash,
            //        Salt = salt,
            //        AggregateId = aggregateId
            //    });

            //    await context.SaveChangesAsync();
            //}
        }
    }

    public sealed class AskAuthServiceStatusConsumer : IConsumer<Contracts.AskForAuthServiceStatus>
    {
        private readonly AuthDbContext _db;

        public AskAuthServiceStatusConsumer(AuthDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<Contracts.AskForAuthServiceStatus> ctx)
        {
            var user = await _db.AuthUsers.AsNoTracking().FirstOrDefaultAsync();
            await ctx.RespondAsync(
                new Contracts.SendAuthServiceStatus(ctx.Message.CorrelationId, "RUNNING")
            );
        }
    }

    public sealed class ValidateCredentialsConsumer : IConsumer<Contracts.ValidateCredentials>
    {
        private readonly AuthDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;

        public ValidateCredentialsConsumer(AuthDbContext db, ITokenService tokenService, IConfiguration config)
        {
            _db = db;
            _tokenService = tokenService;
            _config = config;
        }

        public async Task Consume(ConsumeContext<Contracts.ValidateCredentials> ctx)
        {
            string token = "";

            AuthUser userForEmail = await _db.AuthUsers.SingleOrDefaultAsync(u => u.EmailAddress == ctx.Message.Username);
            byte[] salt = userForEmail!.Salt;

            using var pbkdf2 = new Rfc2898DeriveBytes(
                ctx.Message.Password,
                salt,
                iterations: 100_000,
                HashAlgorithmName.SHA256);

            byte[] hash = pbkdf2.GetBytes(32);

            if (CryptographicOperations.FixedTimeEquals(hash, userForEmail.HashedPassword))
            {
                

                token = _tokenService.BuildToken(
                                        TokenService.getJWTKey(),
                                        "360training.com",
                                        new[]
                                        {
                                            "360training.com"
                                        },
                                        userForEmail.AggregateId.ToString());
            }


            await ctx.RespondAsync(
                new Contracts.CredentialsWereValidated(ctx.Message.CorrelationId, token, true)
            );
        }
    }

    public sealed class CreateAuthUserConsumer : IConsumer<Contracts.CreateAuthUser>
    {
        private readonly AuthDbContext _db;

        public CreateAuthUserConsumer(AuthDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<Contracts.CreateAuthUser> ctx)
        {
            //TODO: consolidate hashing code

            // Generate a salt
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            // Derive a key from the password + salt using PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(
                ctx.Message.password,
                salt,
                iterations: 100_000,
                HashAlgorithmName.SHA256);

            byte[] hash = pbkdf2.GetBytes(32);

            _db.AuthUsers.Add(new AuthUser
            {
                EmailAddress = ctx.Message.email,
                HashedPassword = hash,
                Salt = salt
            });

            await _db.SaveChangesAsync();

            await ctx.RespondAsync(
                new Contracts.AuthUserCreated(ctx.Message.CorrelationId)
            );
        }
    }
}