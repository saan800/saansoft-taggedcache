# .NET Testing Skill

Apply these patterns when writing or reviewing tests in this project.

## Test Projects

Unit and integration tests for the backend code should be added to projects in `/backend/tests/`.
Each test project should reflect the relevant code project.

eg code project at `/backend/src/MonsterList.Identity` should have the test project at `/backend/tests/MonsterList.Identity.Tests`

## Framework & Libraries

- **xUnit** - test framework (`[Fact]`, `[Theory]`, `[InlineData]`)
- **AwesomeAssertions** - use for assertions (`result.Should.Be(...)`)
- **FakeItEasy** - use for mocking interfaces; do NOT mock the database (use real in memory Lite servers instead)

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
dotnet test backend/tests/MonsterList.Identity.Tests/MonsterList.Identity.Tests.csproj

# Filter by name
dotnet test --filter "FullyQualifiedName~TestClassName"
```

## What NOT to Do

- Do not use `Thread.Sleep` - use async/await properly
