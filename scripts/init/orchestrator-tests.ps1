#Go to project root
cd ../../

$TN = "StoreOrchestratorTests"
$SN = "StoreOrchestrator"

dotnet new nunit -n $TN -o tests/$TN
dotnet sln add tests/$TN/$TN.csproj
dotnet add tests/$TN package FluentAssertions
dotnet add tests/$TN/$TN.csproj reference src/$SN/$SN.csproj

#Go back to scripts directory
cd scripts/teardown
