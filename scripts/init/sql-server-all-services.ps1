#Go to project root
cd ../../

$TN = "ApiTests"
$serviceNames = @("StoreOrchestrator","APIGateway","Order","Learner","Auth")

foreach ($SN in $serviceNames) {
    dotnet add src/$SN/$SN.csproj package Aspire.Microsoft.EntityFrameworkCore.SqlServer
    dotnet add src/$SN/$SN.csproj package Aspire.Hosting.SqlServer
}
#Go back to scripts directory
cd scripts/init
