# Code Coverage Guidelines

This document describes the code coverage infrastructure for the Quintessentia project and how to use it.

## Overview

Quintessentia uses **Coverlet** for code coverage collection and **ReportGenerator** for generating detailed HTML reports. Coverage is automatically collected during builds and enforced in the CI/CD pipeline.

## Coverage Requirements

- **Minimum Coverage Threshold**: 70% line coverage
- **Enforcement**: CI/CD builds will fail if coverage falls below the threshold
- **Target Areas**: Services and Controllers (Models and Views are excluded)

## Automatic Coverage (Post-Build)

Coverage is automatically collected when you build the test project in **Debug** configuration:

```bash
dotnet build
```

This will:
1. Build the solution
2. Run all tests
3. Collect coverage data
4. Generate a coverage summary in the build output
5. Create reports in the `coverage/` directory

### Build Output Example

```
========================================
Running Code Coverage Analysis...
Coverage Threshold: 70%
========================================

Test run for Quintessentia.Tests.dll (.NET 9.0)
Microsoft (R) Test Execution Command Line Tool Version 17.x.x
...
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15

========================================
Coverage report generated at: C:\...\coverage
Run 'scripts\view-coverage.ps1' to see detailed HTML report
========================================
```

## Manual Coverage Generation

### Generate Detailed HTML Report

To generate a detailed HTML coverage report:

```powershell
.\scripts\generate-coverage.ps1
```

This script will:
- Run all tests with coverage collection
- Generate HTML reports with detailed file-by-file analysis
- Display coverage statistics
- Automatically open the report in your browser

### Advanced Options

```powershell
# Set custom threshold
.\scripts\generate-coverage.ps1 -Threshold 80

# Fail if below threshold
.\scripts\generate-coverage.ps1 -Threshold 75 -FailOnThreshold $true

# Generate without opening browser
.\scripts\generate-coverage.ps1 -OpenReport $false
```

### View Existing Report

To view the most recent coverage report:

```powershell
.\scripts\view-coverage.ps1
```

## Controlling Coverage Behavior

### Disable Post-Build Coverage

To disable automatic coverage during builds:

```bash
dotnet build -p:RunCoverageOnBuild=false
```

### Set Custom Threshold

```bash
dotnet build -p:CoverageThreshold=80
```

### Fail Build on Low Coverage

```bash
dotnet build -p:FailBuildOnCoverageThreshold=true
```

### Release Builds

Coverage is **automatically disabled** for Release builds to keep them fast:

```bash
dotnet build -c Release  # No coverage collected
```

## CI/CD Integration

### GitHub Actions

The project includes a GitHub Actions workflow (`.github/workflows/dotnet-ci.yml`) that:

1. **Runs on**: Pushes to `main`/`develop` branches and pull requests
2. **Builds** the solution in Release configuration
3. **Runs tests** with coverage collection
4. **Enforces** the 70% minimum threshold
5. **Generates** detailed HTML reports
6. **Uploads** coverage reports as artifacts (30-day retention)
7. **Comments** on PRs with coverage statistics
8. **Fails** the build if coverage is below threshold

### Viewing CI/CD Coverage Reports

1. Go to the Actions tab in GitHub
2. Select the workflow run
3. Download the `coverage-report` artifact
4. Extract and open `index.html`

### Coverage Badge (Optional)

To add a coverage badge to your README:

1. Sign up for [Codecov](https://codecov.io/) (free for open source)
2. Add `CODECOV_TOKEN` to your repository secrets
3. The workflow will automatically upload coverage data
4. Add the badge to your README:

```markdown
[![codecov](https://codecov.io/gh/agorevski/Quintessentia/branch/main/graph/badge.svg)](https://codecov.io/gh/agorevski/Quintessentia)
```

## Coverage Exclusions

The following are automatically excluded from coverage analysis:

### By Assembly Pattern
- `[*.Tests]*` - All test projects
- `[*]*.Program` - Entry point classes
- `[*]*.Views.*` - Razor views
- `[*]*.wwwroot.*` - Static web content

### By File Pattern
- `**\*.cshtml` - Razor view files
- `**\*.css` - Stylesheets
- `**\*.js` - JavaScript files

### Adding Custom Exclusions

To exclude additional code, update the patterns in:
- `tests/Quintessentia.Tests/Quintessentia.Tests.csproj` (post-build target)
- `.github/workflows/dotnet-ci.yml` (CI/CD workflow)
- `scripts/generate-coverage.ps1` (manual script)

Example:
```xml
/p:Exclude="[*.Tests]*,[*]*.Program,[*]*.YourNamespace.*"
```

## Coverage Report Structure

### Report Location
```
coverage/
├── coverage.cobertura.xml      # Machine-readable coverage data
├── coverage.json               # Coverlet raw data
└── report/                     # HTML reports
    ├── index.html              # Main report page
    ├── Summary.txt             # Text summary
    └── ...                     # Detailed per-file reports
```

### HTML Report Features

The HTML report includes:
- **Summary**: Overall coverage statistics
- **Risk Hotspots**: Files with low coverage
- **File Browser**: Detailed line-by-line coverage for each file
- **History**: Coverage trends over time (in CI/CD)
- **Badges**: Visual coverage indicators

## Best Practices

### Writing Testable Code

1. **Use Dependency Injection**: Makes mocking dependencies easier
2. **Follow SOLID Principles**: Especially Single Responsibility
3. **Keep Methods Small**: Easier to test and achieve higher coverage
4. **Avoid Static Dependencies**: Hard to mock in tests

### Improving Coverage

1. **Focus on Services**: Business logic should have highest coverage
2. **Test Edge Cases**: Error conditions, null inputs, boundary values
3. **Use Test Patterns**: Arrange-Act-Assert pattern
4. **Mock External Dependencies**: Azure services, HTTP clients, databases

### Coverage Goals by Component

| Component | Target Coverage | Priority |
|-----------|----------------|----------|
| Services (Business Logic) | 80-90% | High |
| Controllers | 70-80% | Medium |
| Models (POCOs) | Excluded | N/A |
| Views | Excluded | N/A |

## Troubleshooting

### Coverage Not Running After Build

**Problem**: Build completes but coverage doesn't run.

**Solutions**:
- Ensure you're building in Debug configuration: `dotnet build -c Debug`
- Check if coverage is disabled: `dotnet build -p:RunCoverageOnBuild=true`
- Verify the test project builds successfully

### Reports Not Generated

**Problem**: Coverage runs but no HTML report is created.

**Solutions**:
- Install ReportGenerator: `dotnet tool install --global dotnet-reportgenerator-globaltool`
- Update ReportGenerator: `dotnet tool update --global dotnet-reportgenerator-globaltool`
- Check the `coverage/` directory exists

### Coverage Below Threshold

**Problem**: Build fails due to low coverage.

**Solutions**:
1. Run `.\scripts\generate-coverage.ps1` to see detailed report
2. Identify files with low coverage in the HTML report
3. Add tests for uncovered code paths
4. Consider if some code should be excluded

### CI/CD Build Fails

**Problem**: GitHub Actions build fails on coverage check.

**Solutions**:
- Check the Actions log for specific failures
- Run coverage locally to reproduce the issue
- Ensure all tests pass: `dotnet test`
- Verify coverage meets threshold: `.\scripts\generate-coverage.ps1 -FailOnThreshold $true`

## Additional Resources

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [xUnit Testing Best Practices](https://xunit.net/docs/getting-started)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)

## Configuration Files

### Test Project Configuration
- `tests/Quintessentia.Tests/Quintessentia.Tests.csproj`
  - Contains MSBuild post-build target
  - Defines coverage properties and exclusions

### CI/CD Workflow
- `.github/workflows/dotnet-ci.yml`
  - Defines GitHub Actions workflow
  - Enforces coverage threshold in pipeline

### Scripts
- `scripts/generate-coverage.ps1` - Generate detailed coverage reports
- `scripts/view-coverage.ps1` - Open existing coverage reports

## Support

For issues or questions about code coverage:
1. Check this documentation
2. Review the troubleshooting section
3. Check existing issues on GitHub
4. Create a new issue with the `coverage` label
