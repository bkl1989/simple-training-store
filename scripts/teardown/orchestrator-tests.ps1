#Go to project root
cd ../../

$TN = "StoreOrchestratorTests"
$SN = "StoreOrchestrator"

dotnet sln remove tests/$TN/$TN.csproj
Remove-Item -Path tests\StoreOrchestratorTests\ -Recurse -Force

#Go back to scripts directory
cd scripts/teardown