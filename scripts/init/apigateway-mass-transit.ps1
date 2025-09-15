#Go to project root
cd ../../

$SN = "APIGateway"
dotnet add src/$SN/$SN.csproj package Azure.Messaging.ServiceBus
dotnet add src/$SN/$SN.csproj package MassTransit

#Go back to scripts directory
cd scripts/init
