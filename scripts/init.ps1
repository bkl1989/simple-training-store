#
# This is the script that was used to create the basis for the project, before any code was written
#
.\teardown.ps1
Write-Output "Initializing training store application"
dotnet new sln -n SimpleTrainingStore -o SimpleTrainingStore
cd SimpleTrainingStore
#Shared contracts
dotnet new classlib -n Contracts -o src/Contracts
dotnet sln add src/Contracts/Contracts.csproj
#API Gateway, which is the only HTTP interface to the cluster
dotnet new webapi -n APIGateway -o src/APIGateway
dotnet sln add src/APIGateway/APIGateway.csproj
dotnet add src/APIGateway/APIGateway.csproj reference src/Contracts/Contracts.csproj
#Orchestrator, which manages the completion of sagas

$workerServiceNames = @("StoreOrchestrator", "Auth", "Learner", "Order")
$otherServiceNames = @("APIGateway")
$allServiceNames = $workerServiceNames + $otherServiceNames

foreach ($workerServiceName in $workerServiceNames) {
	dotnet new worker -n $workerServiceName -o src/$workerServiceName
	dotnet sln add src/$workerServiceName/$workerServiceName.csproj
	dotnet add src/$workerServiceName/$workerServiceName.csproj reference src/Contracts/Contracts.csproj
}

foreach ($serviceName in $allServiceNames) {
	#Service Testing
	dotnet add src/$serviceName/$serviceName.csproj package NUnit
	dotnet add src/$serviceName/$serviceName.csproj package Microsoft.AspNetCore.Mvc.Testing
	dotnet add src/$serviceName/$serviceName.csproj package Moq
}

#Create test project
mkdir tests
dotnet new nunit -n ApiTests -o tests/ApiTests
dotnet sln add tests/ApiTests/ApiTests.csproj
dotnet add tests/ApiTests package FluentAssertions
dotnet add tests/ApiTests package RestSharp
dotnet add tests/ApiTests/ApiTests.csproj package Microsoft.AspNetCore.TestHost
dotnet add tests/ApiTests/ApiTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/ApiTests/ApiTests.csproj reference src/APIGateway/APIGateway.csproj

mkdir scripts
cd ..
cp .\init.ps1 .\SimpleTrainingStore\scripts\
cp .\teardown.ps1 .\SimpleTrainingStore\scripts\
cp ./APIIntegration.cs ./SimpleTrainingStore/tests/APITests
