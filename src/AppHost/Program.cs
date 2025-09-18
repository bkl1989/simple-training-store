using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Declare resources you want Aspire to orchestrate for local dev.
// (Example SQL resource shown; remove if not needed today.)
var sqlServer = builder.AddSqlServer("sql").WithDataVolume();

var AuthSQL = sqlServer.AddDatabase("AuthDatabase");

var StoreOrchestratorSQL = sqlServer.AddDatabase("StoreOrchestratorDatabase");

var OrderSQL = sqlServer.AddDatabase("OrderDatabase");

var LearnerSQL = sqlServer.AddDatabase("LearnerDatabase");

var user = builder.AddParameter("rabbit-user", secret: true);
var pass = builder.AddParameter("rabbit-pass", secret: true);

var rabbit = builder.AddRabbitMQ("rabbit", user, pass)
                    .WithManagementPlugin();   // management UI container
// Wire projects
builder.AddProject<Projects.APIGateway>("apigateway").WithExternalHttpEndpoints().WithReference(rabbit);
builder.AddProject<Projects.Auth>("auth").WithReference(AuthSQL).WithReference(rabbit);
builder.AddProject<Projects.StoreOrchestrator>("storeorchestrator").WithReference(StoreOrchestratorSQL).WithReference(rabbit);
builder.AddProject<Projects.Order>("order").WithReference(OrderSQL).WithReference(rabbit);
builder.AddProject<Projects.Learner>("learner").WithReference(LearnerSQL).WithReference(rabbit);

builder.Build().Run();
