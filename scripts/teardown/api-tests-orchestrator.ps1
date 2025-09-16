#Go to project root
cd ../../

$TN = "ApiTests"
$SN = "StoreOrchestrator"

dotnet remove tests/$TN/$TN.csproj reference src/$SN/$SN.csproj

#Go back to scripts directory
cd scripts/teardown