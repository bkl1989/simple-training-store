mkdir tests
dotnet new nunit -n ApiTests -o tests/ApiTests
dotnet sln add tests/ApiTests/ApiTests.csproj
dotnet add tests/ApiTests package FluentAssertions
dotnet add tests/ApiTests package RestSharp
dotnet add tests/ApiTests/ApiTests.csproj package Microsoft.AspNetCore.TestHost
dotnet add tests/ApiTests/ApiTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/ApiTests/ApiTests.csproj reference src/APIGateway/APIGateway.csproj
