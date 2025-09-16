#Go to project root
cd ../../

$serviceNames = @("Auth", "Learner","Order");

foreach ($SN in $serviceNames) {
    dotnet add src/$SN/$SN.csproj package Azure.Messaging.ServiceBus
    dotnet add src/$SN/$SN.csproj package MassTransit
}
#Go back to scripts directory
cd scripts/init
