using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Declare resources you want Aspire to orchestrate for local dev.
// (Example SQL resource shown; remove if not needed today.)
var sql = builder.AddSqlServer("sql")
                 .WithDataVolume()
                 .AddDatabase("sqldata");

// Wire projects (adjust names to match your solution).
builder.AddProject<Projects.APIGateway>("apigateway")
       .WithExternalHttpEndpoints();

// If you have other services, add them too. Example:
// builder.AddProject<Projects.Auth>("auth").WithReference(sql);
// builder.AddProject<Projects.StoreOrchestrator>("storeorchestrator");

builder.Build().Run();
