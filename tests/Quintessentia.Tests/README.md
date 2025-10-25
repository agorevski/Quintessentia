# Quintessentia Test Suite

This directory contains comprehensive tests for the Quintessentia application, covering both automated unit tests and manual UX testing procedures.

## Test Overview

### Automated Tests
- **AudioServiceTests.cs** - Unit tests for the `AudioService` class covering download, caching, and AI processing pipeline
- **AudioControllerTests.cs** - Unit tests for the `AudioController` covering HTTP endpoints, validation, and error handling

### Manual Tests
- **MANUAL_UX_TEST_CHECKLIST.md** - Comprehensive 50-point UX testing checklist covering visual design, interactions, accessibility, and browser compatibility

## Test Statistics

### Functional Test Cases: 28 Total
- Audio Download & Caching: 7 tests
- AI Processing Pipeline: 7 tests
- Custom Settings Override: 6 tests
- Server-Sent Events (SSE): 4 tests
- Storage Services: 4 tests

### UX Test Cases: 50 Total
- Visual Design & Layout: 4 tests
- Form Interaction: 4 tests
- Processing Feedback: 8 tests
- Settings Modal: 11 tests
- Result Page UX: 5 tests
- Error Handling UX: 4 tests
- Performance & Loading: 3 tests
- Accessibility: 4 tests
- Mobile Experience: 3 tests
- Browser Compatibility: 4 tests

## Running the Tests

### Prerequisites

1. **.NET 9 SDK** installed
2. **Azure OpenAI credentials** configured (for integration testing)
3. **Test project dependencies** installed:
   - xUnit
   - Moq
   - FluentAssertions
   - Microsoft.AspNetCore.Mvc.Testing

### Run All Automated Tests

```bash
# From the Quintessentia.Tests directory
dotnet test

# With detailed output
dotnet test --verbosity detailed

# With code coverage (if enabled)
dotnet test --collect:"XPlat Code Coverage"
```

### Run Specific Test Class

```bash
# Run only AudioService tests
dotnet test --filter "FullyQualifiedName~AudioServiceTests"

# Run only AudioController tests
dotnet test --filter "FullyQualifiedName~AudioControllerTests"
```

### Run Specific Test Method

```bash
# Run a single test
dotnet test --filter "FullyQualifiedName~GetOrDownloadEpisodeAsync_WithValidUrl_DownloadsSuccessfully"
```

## Test Organization

### Unit Tests Structure

Tests follow the **Arrange-Act-Assert (AAA)** pattern:

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var testUrl = "https://example.com/test.mp3";
    _mockService.Setup(s => s.Method()).Returns(value);
    
    // Act - Execute the method under test
    var result = await _service.MethodUnderTest(testUrl);
    
    // Assert - Verify the expected outcome
    result.Should().NotBeNull();
    result.Success.Should().BeTrue();
}
```

### Test Coverage by Functional Area

#### TC-F-001 to TC-F-007: Download & Caching
- Valid URL download
- Cache hit scenarios
- Invalid URL handling
- 404 error handling
- Large file handling
- Cache key generation

#### TC-F-008 to TC-F-014: AI Pipeline
- Full pipeline execution
- Whisper transcription
- GPT summarization
- TTS generation
- Summary caching
- API failure handling
- Partial failure recovery

#### TC-F-015 to TC-F-020: Custom Settings
- Endpoint override
- Custom deployment names
- TTS speed adjustment
- Audio format selection
- Settings persistence
- Reset functionality

#### TC-F-021 to TC-F-024: SSE
- Real-time progress updates
- Connection error handling
- Multiple stage updates
- Completion redirect

#### TC-F-025 to TC-F-028: Storage
- Blob upload/download
- Metadata persistence
- Container configuration

#### TC-F-029 to TC-F-033: Result Page
- Audio player display
- Summary information
- Word counts
- File downloads

## Manual Testing

For comprehensive UX testing, follow the checklist in `MANUAL_UX_TEST_CHECKLIST.md`. This covers:

1. **Visual Design** - Responsive layout, branding, shadows
2. **Form Interaction** - Input validation, focus states, placeholders
3. **Processing Feedback** - Progress bars, status updates, loading states
4. **Settings Modal** - Custom configurations, persistence
5. **Result Page** - Audio players, word counts, download functionality
6. **Error Handling** - User-friendly messages, recovery
7. **Performance** - Load times, large file handling
8. **Accessibility** - Keyboard navigation, screen readers, contrast
9. **Mobile** - Touch targets, responsive modal, audio controls
10. **Browser Compatibility** - Chrome, Firefox, Safari, Edge

### Sample Test URL

For manual testing, use this public sample MP3:
```
https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3
```

## Test Data & Mocking

### Mock Setup

Tests use Moq to mock dependencies:

```csharp
// Mock HTTP responses
var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
mockHttpMessageHandler
    .Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
    )
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new ByteArrayContent(testData)
    });

// Mock service methods
_audioServiceMock
    .Setup(a => a.GetOrDownloadEpisodeAsync(It.IsAny<string>()))
    .ReturnsAsync("/temp/audio.mp3");
```

### Test Assertions

Tests use FluentAssertions for readable assertions:

```csharp
// Standard assertions
result.Should().NotBeNull();
result.Success.Should().BeTrue();
result.Message.Should().Contain("success");

// Collection assertions
progressUpdates.Should().NotBeEmpty();
progressUpdates.Should().Contain(s => s.Stage == "transcribing");

// Type assertions
var viewResult = result.Should().BeOfType<ViewResult>().Subject;
var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
```

## Continuous Integration

### GitHub Actions / Azure DevOps

Example CI configuration:

```yaml
- name: Run Tests
  run: dotnet test --no-build --verbosity normal
  
- name: Generate Coverage Report
  run: dotnet test --collect:"XPlat Code Coverage"
  
- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    files: ./coverage.xml
```

## Test Maintenance

### Adding New Tests

1. Identify the functionality to test
2. Determine test category (service, controller, integration)
3. Follow naming convention: `MethodName_Scenario_ExpectedBehavior`
4. Add to appropriate test class
5. Update this README with test count
6. Map to functional test case ID (TC-F-XXX)

### Updating Existing Tests

1. Review test after code changes
2. Update mocks if dependencies changed
3. Verify assertions still valid
4. Update test documentation

## Known Limitations

### Unit Tests
- Storage service tests use in-memory mocks (not actual Azure Blob Storage)
- Azure OpenAI service is mocked (no real API calls)
- File I/O operations use temporary directories

### Manual Tests
- Azure OpenAI credentials required for full pipeline testing
- Large file testing depends on available bandwidth
- Browser compatibility testing requires multiple browsers/devices

## Troubleshooting

### Tests Fail with "Connection Refused"
- Ensure no real HTTP calls are being made
- Verify all external services are properly mocked

### Tests Fail with File Path Issues
- Check that temp directory is accessible
- Verify file cleanup in test teardown

### Flaky Tests
- Look for timing-dependent assertions
- Consider adding appropriate delays or using Task.WaitAll
- Check for shared state between tests

## Contributing

When adding new functionality:
1. Write tests first (TDD approach recommended)
2. Ensure tests cover happy path and error cases
3. Add both unit tests and update manual checklist if UX is affected
4. Maintain test coverage above 80%
5. Document any new test patterns or utilities

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [ASP.NET Core Testing](https://docs.microsoft.com/en-us/aspnet/core/test/)
- [WCAG Accessibility Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

## Contact

For questions about the test suite, please contact the development team or refer to the main project README.
