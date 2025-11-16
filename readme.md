# Quintessentia

**Distilling audio down to its pure essence** - Quintessentia is an AI-powered audio summarization tool that transforms lengthy audio episodes into concise 5-minute summaries. It downloads audio files, transcribes the content, generates an intelligent summary, and produces a new audio file containing just the essential information.

## What It Does

Quintessentia processes audio episodes through a complete AI pipeline:

1. **Download & Cache** - Downloads MP3 audio episodes from direct URLs and caches them locally for efficient reprocessing
2. **Transcribe** - Converts audio to text using Azure OpenAI Whisper
3. **Summarize** - Uses GPT to intelligently condense the transcript to approximately 5 minutes of content
4. **Generate Audio** - Creates a new audio file of the summary using text-to-speech (TTS)

The result is an audio episode "boiled down" to its bare essence - perfect for quickly understanding the key points of long-form content.

## Project Structure

### Controllers/

- **HomeController.cs** - Serves the landing page and handles basic navigation
- **AudioController.cs** - Main processing endpoints:
  - `Process` - Download-only endpoint (no AI processing)
  - `ProcessAndSummarize` - Full AI pipeline endpoint
  - `ProcessAndSummarizeStream` - Server-Sent Events (SSE) endpoint for real-time progress updates
  - `Result` - Displays processing results with audio playback

### Services/

- **AudioService.cs** - Core orchestration service:
  - Episode download and caching (SHA-256 hash-based cache keys)
  - MP3 file streaming and storage
  - Full AI processing pipeline coordination
  - Progress callback system for real-time status updates
- **AzureOpenAIService.cs** - Azure OpenAI integration:
  - Audio transcription using Whisper
  - Transcript summarization using GPT
  - Speech generation using TTS (text-to-speech)
- **IAudioService.cs** & **IAzureOpenAIService.cs** - Service interfaces

### Models/

- **ProcessingStatus.cs** - Real-time processing status for SSE streaming
- **PodcastProcessResult.cs** - Complete processing result data
- **ErrorViewModel.cs** - Error page model

### Views/

- **Home/Index.cshtml** - Main landing page with audio URL input form and real-time progress tracking
- **Audio/Result.cshtml** - Results page displaying transcript, summary text, and audio players
- **Shared/_Layout.cshtml** - Application layout template
- **Shared/Error.cshtml** - Error page

### wwwroot/

- **css/** - Custom stylesheets (site.css)
- **js/** - Client-side JavaScript (site.js)
- **lib/** - Third-party libraries:
  - Bootstrap 5 (UI framework)
  - jQuery (DOM manipulation)
  - Bootstrap Icons (icon library)

## Key Features

### Real-Time Progress Tracking

The application uses Server-Sent Events (SSE) to stream processing status updates to the client in real-time. Users can watch as their audio moves through each stage:

- Downloading
- Transcribing
- Summarizing
- Generating speech

### Intelligent Caching

- **Episode Cache**: Original MP3 files are cached using SHA-256 hashes of their URLs
- **Summary Cache**: Processed summaries are cached to avoid reprocessing
- **Cache Location**: `%LocalAppData%\SpotifySummarizer\PodcastCache`

### Flexible Processing Options

- **Full Pipeline**: Download → Transcribe → Summarize → Generate Audio
- **Download Only**: Just cache the original MP3 without AI processing

## Technology Stack

- **.NET 9** - Latest version of the .NET framework
- **ASP.NET Core MVC** - Web application framework
- **Azure OpenAI** - AI services for transcription, summarization, and TTS
- **Bootstrap 5** - Responsive UI framework
- **Server-Sent Events (SSE)** - Real-time progress updates
- **C# 12** - Programming language
- **xUnit** - Testing framework
- **Coverlet** - Code coverage collection
- **ReportGenerator** - Coverage report generation

## Setup Requirements

To run Quintessentia, you need:

1. **.NET 9 SDK** installed
2. **Azure OpenAI** credentials configured in `appsettings.json`:
   - Endpoint URL
   - API Key
   - Deployment names for Whisper, GPT, and TTS models

```cmd
az login --tenant 8a106375-957e-4690-978b-a81220e49845
```

See `README_AZURE_SETUP.md` for detailed Azure OpenAI configuration instructions.

## Running the Application

```bash
dotnet run
```

The application will start on `https://localhost` (port assigned by Kestrel). Navigate to the homepage, paste a direct MP3 URL, and click "Process & Summarize" to begin.

## Testing & Code Coverage

Quintessentia maintains a **minimum of 70% code coverage** for all releases.

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage (automatically happens on Debug builds)
dotnet build
```

### Generate Coverage Reports

```powershell
# Generate detailed HTML coverage report
.\scripts\generate-coverage.ps1

# View existing coverage report
.\scripts\view-coverage.ps1
```

### Coverage Features

- **Automatic Post-Build Coverage**: Coverage is collected automatically when building in Debug configuration
- **CI/CD Enforcement**: GitHub Actions enforces the 70% minimum threshold on all PRs and merges
- **Detailed Reports**: HTML reports show line-by-line coverage with risk hotspot analysis
- **Real-Time Feedback**: Build output includes coverage statistics

For detailed information about code coverage, see [Code Coverage Guidelines](docs/CODE_COVERAGE.md).

### Testing Documentation

- [Test Project README](tests/Quintessentia.Tests/README.md) - Testing architecture and patterns
- [Manual UX Test Checklist](tests/Quintessentia.Tests/MANUAL_UX_TEST_CHECKLIST.md) - User experience testing guide
- [Code Coverage Guidelines](docs/CODE_COVERAGE.md) - Complete coverage documentation

## Output Files

For each processed audio episode (identified by cache key), the following files are created in the cache directory:

- `{episodeId}.mp3` - Original downloaded episode
- `{episodeId}_transcript.txt` - Full text transcription
- `{episodeId}_summary.txt` - Summarized text (target: ~5 minutes)
- `{episodeId}_summary.mp3` - Generated audio of the summary

## Future Enhancements

Potential improvements for Quintessentia:

- Support for streaming service URLs with automatic MP3 extraction
- Customizable summary lengths
- Multiple voice options for TTS
- Batch processing of multiple episodes
- RSS feed integration
- Summary highlights and key takeaways extraction
