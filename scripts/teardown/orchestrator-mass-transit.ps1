#Go to project root
cd ../../

$SN = "StoreOrchestrator"
dotnet remove src/$SN/$SN.csproj package Azure.Messaging.ServiceBus
dotnet remove src/$SN/$SN.csproj package MassTransit

#Go back to scripts directory
cd scripts/init