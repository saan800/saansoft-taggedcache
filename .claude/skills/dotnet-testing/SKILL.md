# .NET Testing Skill

Apply these patterns when writing or reviewing tests in this project.

All tests must to be able to run in GitHub workflows for CI.

## Test Projects

Unit and integration tests should be added to projects in `tests/`.
Each test project should reflect the relevant code project folder structure.

eg code project at `/src/SaanSoft.TaggedCache` should have the test project at `/tests/SaanSoft.Tests.TaggedCache`

### Testing strategy for infrastructure specific implementations

`SaanSoft.TaggedCache.AwsDynamoDb`, `SaanSoft.TaggedCache.Memory` and `SaanSoft.TaggedCache.StackExchangeRedis` are implementations 
of the same `ITaggedCache` based on `BaseTaggedCache`, just for different infrastructure.

Their logical implementations should match, and test cases should be the same, just using a different cache store infrastructure.

Where possible, generic tests should be written in `SaanSoft.Tests.TaggedCache` with a `ITaggedCache sut` parameter that is then
configured by the infrastructure specific test projects.

The test projects should setup `ITaggedCache sut` using `Testcontainers` for the required infrastructure.

## Framework & Libraries

- **xUnit** - test framework (`[Fact]`, `[Theory]`, `[InlineData]`)
- **AwesomeAssertions** - use for assertions (`result.Should.Be(...)`)
- **FakeItEasy** - use for mocking interfaces; do NOT mock the database (use real in memory Lite servers instead)
- **Testcontainers** - use for testing with lite / in memory cache store for the required infrastructure

## Test Naming

```
MethodName_Scenario_ExpectedResult
// e.g. GetUser_WhenUserNotFound_Returns404
```

## Running Tests

```bash
# All tests
dotnet test

# Single project
dotnet test tests/SaanSoft.Tests.TaggedCache/SaanSoft.Tests.TaggedCache.csproj

# Filter by name
dotnet test --filter "FullyQualifiedName~TestClassName"
```

## What NOT to Do

- Do not use `Thread.Sleep` - use async/await properly
