#Go to project root
cd ../../

$TN = "ApiTests"
$serviceNames = @("StoreOrchestrator","APIGateway","Order","Learner","Auth")

foreach ($SN in $serviceNames) {
    dotnet add src/$SN/$SN.csproj package Microsoft.EntityFrameworkCore.Design
}
#Go back to scripts directory
cd scripts/init
