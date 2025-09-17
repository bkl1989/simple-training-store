using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Declare resources you want Aspire to orchestrate for local dev.
// (Example SQL resource shown; remove if not needed today.)
var sql = builder.AddSqlServer("sql")
                 .WithDataVolume()
                 .AddDatabase("sqldata");

// Wire projects
builder.AddProject<Projects.APIGateway>("apigateway").WithExternalHttpEndpoints();
builder.AddProject<Projects.Auth>("auth").WithReference(sql);
builder.AddProject<Projects.StoreOrchestrator>("storeorchestrator");
builder.AddProject<Projects.Order>("order");
builder.AddProject<Projects.Learner>("learner");

builder.Build().Run();
