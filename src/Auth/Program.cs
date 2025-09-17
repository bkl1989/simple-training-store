using Auth;
using Google.Protobuf.WellKnownTypes;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

var builder = Host.CreateApplicationBuilder(args);
builder.AddSqlServerDbContext<UserDBContext>("sqldata");

// Keep your worker (optional)
builder.Services.AddHostedService<Auth.Worker>();

// MassTransit: respond to AskForAuthServiceStatus
builder.Services.AddMassTransit(mt =>
{
    mt.SetKebabCaseEndpointNameFormatter();

    mt.AddConsumer<AskAuthServiceStatusConsumer>();

    mt.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddDbContext<UserDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("sqldata")));

builder.Build().Run();

var host = builder.Build();

if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    using (var scope = host.Services.CreateScope())
    {
        UserDBContext context = scope.ServiceProvider.GetRequiredService<UserDBContext>();

        context.Database.EnsureCreated();

        if (!await context.Users.AnyAsync())
        {
            string password = "123Password";

            byte[] salt = RandomNumberGenerator.GetBytes(16);

            // Derive a key from the password + salt using PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations: 100_000,
                HashAlgorithmName.SHA256);

            byte[] hash = pbkdf2.GetBytes(32);

            context.Users.AddRange(
                new User { EmailAddress = "bkl1989@gmail.com", HashedPassword = hash }
            );

            await context.SaveChangesAsync();
        }
    }
}
else
{

}

host.Run();

public sealed class AskAuthServiceStatusConsumer : IConsumer<Contracts.AskForAuthServiceStatus>
{
    private readonly UserDBContext _db;

    public AskAuthServiceStatusConsumer(UserDBContext db)
    {
        _db = db;
    }
    public async Task Consume(ConsumeContext<Contracts.AskForAuthServiceStatus> ctx)
    {
        //how do I get the host in this context?
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync();
        await ctx.RespondAsync(new Contracts.SendAuthServiceStatus(ctx.Message.CorrelationId, "RUNNING"));
    }
}
