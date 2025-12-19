# Quintessentia

**Distilling audio down to its pure essence** - Quintessentia is an AI-powered audio summarization tool that transforms lengthy audio episodes into concise 5-minute summaries. It downloads audio files, transcribes the content, generates an intelligent summary, and produces a new audio file containing just the essential information.

## What It Does

Quintessentia processes audio episodes through a complete AI pipeline:

1. **Download & Cache** - Downloads MP3 audio episodes from direct URLs and caches them in Azure Blob Storage (or locally in development)
2. **Transcribe** - Converts audio to text using Azure OpenAI Whisper
3. **Summarize** - Uses GPT to intelligently condense the transcript to approximately 5 minutes of content
4. **Generate Audio** - Creates a new audio file of the summary using text-to-speech (TTS)

The result is an audio episode "boiled down" to its bare essence - perfect for quickly understanding the key points of long-form content.

## Project Structure

```
src/Quintessentia/
├── Controllers/          # MVC controllers
├── Models/               # Data models and view models
├── Services/             # Business logic services
│   └── Contracts/        # Service interfaces
├── Utilities/            # Shared helper utilities
├── Views/                # Razor views
└── wwwroot/              # Static files (CSS, JS, images)

tests/Quintessentia.Tests/
├── Controllers/          # Controller tests
├── Models/               # Model tests
└── Services/             # Service tests
```

### Key Components

- **AudioController** - Main processing endpoints including SSE streaming for real-time progress
- **AudioService** - Core orchestration for episode download, caching, and AI pipeline
- **AzureOpenAIService** - Azure OpenAI integration for transcription, summarization, and TTS
- **StorageService** - Abstraction for Azure Blob Storage (production) or local file storage (development)
- **TextHelper** - Shared text manipulation utilities

## Key Features

### Real-Time Progress Tracking

The application uses Server-Sent Events (SSE) to stream processing status updates to the client in real-time.

### Intelligent Caching

- **Episode Cache**: Original MP3 files are cached using SHA-256 hashes of their URLs
- **Summary Cache**: Processed summaries are cached to avoid reprocessing
- **Storage**: Azure Blob Storage (production) or local file system (development)

### Environment-Based Services

- **Development**: Uses mock services for faster iteration without Azure costs
- **Production**: Uses real Azure services (Blob Storage, OpenAI)

## Technology Stack

- **.NET 9** / **C# 12**
- **ASP.NET Core MVC**
- **Azure OpenAI** - Whisper, GPT, and TTS models
- **Azure Blob Storage** - Episode and summary caching
- **Bootstrap 5** - Responsive UI
- **Server-Sent Events (SSE)** - Real-time progress
- **xUnit** - Testing framework
- **Coverlet** - Code coverage

## Setup Requirements

1. **.NET 9 SDK** installed
2. **Azure OpenAI** credentials configured via environment variables or `appsettings.json`
3. **Azure Storage** connection string (for production)

> ⚠️ **Security Note**: Never commit secrets to source control. Use environment variables, Azure Key Vault, or user secrets for sensitive configuration.

## Running the Application

```bash
dotnet run --project src/Quintessentia
```

Navigate to `https://localhost:{port}`, paste a direct MP3 URL, and click "Process & Summarize".

## Testing & Code Coverage

Quintessentia maintains a **minimum of 70% code coverage**.

```bash
# Run all tests
dotnet test

# Generate coverage report
.\scripts\generate-coverage.ps1
```

## Documentation

- [Code Coverage Guidelines](docs/CODE_COVERAGE.md)
- [Test Project README](tests/Quintessentia.Tests/README.md)
- [Manual UX Test Checklist](tests/Quintessentia.Tests/MANUAL_UX_TEST_CHECKLIST.md)
- [Anti-Patterns Analysis](ANTI-PATTERNS.md)
