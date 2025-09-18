#Go to project root
cd ../../

$TN = "ApiTests"
$serviceNames = @("StoreOrchestrator","APIGateway","Order","Learner","Auth")

foreach ($SN in $serviceNames) {
    dotnet add src/$SN/$SN.csproj package Microsoft.EntityFrameworkCore.Sqlite
    dotnet add src/$SN/$SN.csproj package Microsoft.Data.Sqlite
}
#Go back to scripts directory
cd scripts/init
