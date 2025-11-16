# Code Coverage Baseline Report

**Generated**: November 16, 2025
**Current Coverage**: 20.5% line coverage

## Summary

The code coverage infrastructure has been successfully implemented for the Quintessentia project. This report provides the baseline coverage metrics and identifies areas for improvement.

## Current Coverage Metrics

- **Line Coverage**: 20.5% (411 of 2002 coverable lines)
- **Branch Coverage**: 14.3% (65 of 452 branches)
- **Method Coverage**: 36.1% (56 of 155 methods)

## Coverage by Component

### Well-Covered Components ✅

| Component | Coverage | Status |
|-----------|----------|--------|
| AudioService | 93.6% | Excellent |
| AudioProcessResult | 91.6% | Excellent |
| AudioEpisode | 100% | Perfect |
| AudioSummary | 100% | Perfect |

### Partially Covered Components ⚠️

| Component | Coverage | Status |
|-----------|----------|--------|
| AudioController | 40.1% | Needs Work |
| ProcessingStatus | 64.2% | Good |
| AzureOpenAISettings | 44.4% | Needs Work |
| ErrorViewModel | 33.3% | Needs Work |

### Uncovered Components ❌

The following components have 0% coverage:

**Controllers:**
- HomeController

**Services:**
- AzureBlobMetadataService
- AzureBlobStorageService
- AzureOpenAIService
- CacheKeyService
- EpisodeQueryService
- LocalFileMetadataService
- LocalFileStorageService
- MockAzureOpenAIService
- ProcessingProgressService
- StorageConfiguration

**Infrastructure:**
- Program.cs
- All Razor Views

## Analysis

### Why Coverage is Low

1. **Views Included**: Razor views (which should be excluded) are counted in coverage
2. **Program.cs Included**: Application entry point is counted
3. **Missing Service Tests**: Most services have no test coverage yet
4. **Storage Services**: Local and Azure storage services are untested

### Expected Coverage After Exclusions

If we exclude views, Program.cs, and wwwroot as configured in the coverage settings, the effective coverage would be:

**Testable Code:**
- Controllers: ~40% (only AudioController partially tested)
- Services: ~30% (only AudioService well tested)
- Models: ~75% (most models are well covered)

**Estimated Adjusted Coverage**: ~35-40%

## Recommendations

### Priority 1: Add Service Tests

Create tests for critical services:
1. **MockAzureOpenAIService** - Used in development, should be tested
2. **LocalFileStorageService** - Core caching functionality
3. **LocalFileMetadataService** - Metadata management
4. **CacheKeyService** - Cache key generation

### Priority 2: Improve Controller Coverage

1. **HomeController** - Add tests for all actions
2. **AudioController** - Increase coverage from 40% to 70%+
   - Test error scenarios
   - Test SSE streaming
   - Test validation

### Priority 3: Test Remaining Services

1. **ProcessingProgressService** - Progress tracking
2. **EpisodeQueryService** - Query functionality
3. **StorageConfiguration** - Configuration handling

### Priority 4: Azure Service Tests

Add tests for Azure services (or mark as excluded if integration tests only):
1. **AzureOpenAIService**
2. **AzureBlobStorageService**
3. **AzureBlobMetadataService**

## Path to 70% Coverage

To reach the 70% threshold:

1. **Verify Exclusions Work**: Ensure views, Program.cs, and wwwroot are excluded from coverage calculations
2. **Add 15-20 Service Tests**: Cover the untested services
3. **Improve Controller Tests**: Bring AudioController to 70%+, add HomeController tests
4. **Test Edge Cases**: Add tests for error conditions and boundary cases

**Estimated Effort**: 8-12 hours of test development

## Infrastructure Status

### ✅ Completed

- [x] Coverlet integration (coverage collection)
- [x] ReportGenerator setup (HTML reports)
- [x] PowerShell scripts for manual coverage generation
- [x] GitHub Actions workflow with coverage enforcement
- [x] Post-build coverage collection (Debug builds)
- [x] Coverage exclusion patterns configured
- [x] Documentation (CODE_COVERAGE.md)
- [x] README updated with coverage information

### ⚠️ Issues to Resolve

1. **MSBuild Warning**: `Property is not valid` warning in post-build target
   - Cause: Square bracket escaping in Exclude patterns
   - Impact: Exclusions may not be applied correctly
   - **Action Required**: Test and fix exclusion patterns

2. **Post-Build Target**: Currently not generating reports automatically
   - Cause: MSBuild property parsing issue
   - Workaround: Use `dotnet test --collect:"XPlat Code Coverage"` directly
   - **Action Required**: Fix MSBuild Exec command escaping

## Next Steps

1. **Fix Post-Build Target**: Resolve the MSBuild escaping issue so coverage runs automatically
2. **Verify Exclusions**: Confirm views, Program.cs, and wwwroot are excluded
3. **Add Priority Tests**: Focus on MockAzureOpenAIService and storage services
4. **CI/CD Verification**: Push changes to trigger GitHub Actions workflow
5. **Set Realistic Threshold**: Consider starting with 50% and increasing to 70% over time

## Coverage Report Location

- **HTML Report**: `coverage/report/index.html`
- **Text Summary**: `coverage/report/Summary.txt`
- **Cobertura XML**: `tests/Quintessentia.Tests/TestResults/[guid]/coverage.cobertura.xml`

## Commands

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:tests\Quintessentia.Tests\TestResults\**\coverage.cobertura.xml -targetdir:coverage\report -reporttypes:Html

# View coverage
.\scripts\view-coverage.ps1

# Generate with script (once fixed)
.\scripts\generate-coverage.ps1
```

---

**Note**: This is a baseline report. Coverage will improve as more tests are added. The goal is to reach and maintain 70% coverage for all production code (excluding views, Program.cs, and static content).
