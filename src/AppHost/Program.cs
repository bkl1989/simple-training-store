using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Declare resources you want Aspire to orchestrate for local dev.
// (Example SQL resource shown; remove if not needed today.)
var sql = builder.AddSqlServer("sql")
                 .WithDataVolume()
                 .AddDatabase("sqldata");

var user = builder.AddParameter("rabbit-user", secret: true);
var pass = builder.AddParameter("rabbit-pass", secret: true);

var rabbit = builder.AddRabbitMQ("rabbit", user, pass)
                    .WithManagementPlugin();   // management UI container
// Wire projects
builder.AddProject<Projects.APIGateway>("apigateway").WithExternalHttpEndpoints().WithReference(rabbit);
builder.AddProject<Projects.Auth>("auth").WithReference(sql).WithReference(rabbit);
builder.AddProject<Projects.StoreOrchestrator>("storeorchestrator").WithReference(rabbit);
builder.AddProject<Projects.Order>("order").WithReference(rabbit);
builder.AddProject<Projects.Learner>("learner").WithReference(rabbit);

builder.Build().Run();
