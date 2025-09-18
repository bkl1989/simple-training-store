#Go to project root
cd ../../

$TN = "ApiTests"
$serviceNames = @("Contracts","AppHost","StoreOrchestrator","APIGateway","Order","Learner","Auth")

foreach ($SN in $serviceNames) {
    dotnet add src/$SN/$SN.csproj package Aspire.Hosting.Azure.KeyVault
    dotnet add src/$SN/$SN.csproj package Aspire.Azure.Security.KeyVault
}
#Go back to scripts directory
cd scripts/init
