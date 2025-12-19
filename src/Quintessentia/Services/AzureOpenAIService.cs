using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class AzureOpenAIService : IAzureOpenAIService
    {
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AzureOpenAIClient _sttClient;
        private readonly AzureOpenAIClient _gptClient;
        private readonly AzureOpenAIClient _ttsClient;
        private readonly string _sttDeployment;
        private readonly string _gptDeployment;
        private readonly string _ttsDeployment;
        
        // Azure OpenAI Whisper API has a 25MB file size limit - use smaller files to get all of the results faster
        private const long MAX_AUDIO_FILE_SIZE = 5 * 1024 * 1024; // 5MB to leave buffer
        private const int CHUNK_OVERLAP_SECONDS = 1; // Overlap to avoid losing words at boundaries

        public AzureOpenAIService(ILogger<AzureOpenAIService> logger, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;

            // Initialize Speech-to-Text client
            var sttEndpoint = _configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("Speech-to-Text endpoint not configured");
            var sttKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("Speech-to-Text key not configured");
            _sttDeployment = _configuration["AzureOpenAI:SpeechToText:DeploymentName"] 
                ?? throw new InvalidOperationException("Speech-to-Text deployment name not configured");
            _sttClient = new AzureOpenAIClient(new Uri(sttEndpoint), new AzureKeyCredential(sttKey));

            // Initialize GPT client
            var gptEndpoint = _configuration["AzureOpenAI:Endpoint"] 
                ?? throw new InvalidOperationException("GPT endpoint not configured");
            var gptKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("GPT key not configured");
            _gptDeployment = _configuration["AzureOpenAI:GPT:DeploymentName"] 
                ?? throw new InvalidOperationException("GPT deployment name not configured");
            _gptClient = new AzureOpenAIClient(new Uri(gptEndpoint), new AzureKeyCredential(gptKey));

            // Initialize Text-to-Speech client
            var ttsEndpoint = _configuration["AzureOpenAI:Endpoint"] 
                ?? throw new InvalidOperationException("Text-to-Speech endpoint not configured");
            var ttsKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("Text-to-Speech key not configured");
            _ttsDeployment = _configuration["AzureOpenAI:TextToSpeech:DeploymentName"] 
                ?? throw new InvalidOperationException("Text-to-Speech deployment name not configured");
            _ttsClient = new AzureOpenAIClient(new Uri(ttsEndpoint), new AzureKeyCredential(ttsKey));

            _logger.LogInformation("AzureOpenAIService initialized with deployments - STT: {STT}, GPT: {GPT}, TTS: {TTS}",
                _sttDeployment, _gptDeployment, _ttsDeployment);
        }

        private Models.AzureOpenAISettings? GetCustomSettings()
        {
            return _httpContextAccessor.HttpContext?.Items["AzureOpenAISettings"] as Models.AzureOpenAISettings;
        }

        private (AzureOpenAIClient client, string deployment) GetSTTClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.WhisperDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.WhisperDeployment ?? _sttDeployment;
                
                _logger.LogInformation("Using custom STT settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_sttClient, _sttDeployment);
        }

        private (AzureOpenAIClient client, string deployment) GetGPTClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.GptDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.GptDeployment ?? _gptDeployment;
                
                _logger.LogInformation("Using custom GPT settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_gptClient, _gptDeployment);
        }

        private (AzureOpenAIClient client, string deployment) GetTTSClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.TtsDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.TtsDeployment ?? _ttsDeployment;
                
                _logger.LogInformation("Using custom TTS settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_ttsClient, _ttsDeployment);
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting transcription for file: {FilePath}", audioFilePath);

                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(audioFilePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {audioFilePath}");
                }

                var fileInfo = new FileInfo(audioFilePath);
                var fileSize = fileInfo.Length;

                _logger.LogInformation("Audio file size: {Size} bytes ({SizeMB:F2} MB)", fileSize, fileSize / (1024.0 * 1024.0));

                // Check if file exceeds size limit
                if (fileSize > MAX_AUDIO_FILE_SIZE)
                {
                    _logger.LogWarning("File size ({Size} bytes) exceeds limit ({Limit} bytes). Will process in chunks.", fileSize, MAX_AUDIO_FILE_SIZE);
                    return await TranscribeAudioInChunksAsync(audioFilePath, cancellationToken);
                }

                // Process single file
                return await TranscribeSingleAudioFileAsync(audioFilePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transcribing audio file: {FilePath}", audioFilePath);
                throw;
            }
        }

        private async Task<string> TranscribeSingleAudioFileAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            var (client, deployment) = GetSTTClientAndDeployment();
            var audioClient = client.GetAudioClient(deployment);

            using var audioStream = File.OpenRead(audioFilePath);
            var transcriptionOptions = new AudioTranscriptionOptions
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
                Temperature = 0.0f
            };

            var result = await audioClient.TranscribeAudioAsync(audioStream, audioFilePath, transcriptionOptions);

            var transcript = result.Value.Text;
            _logger.LogInformation("Transcription completed. Length: {Length} characters", transcript.Length);

            return transcript;
        }

        private async Task<string> TranscribeAudioInChunksAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            var tempChunkDirectory = Path.Combine(Path.GetTempPath(), $"audio_chunks_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempChunkDirectory);

            try
            {
                _logger.LogInformation("Chunking audio file into smaller segments...");

                cancellationToken.ThrowIfCancellationRequested();

                // Get audio duration using FFprobe
                var duration = await GetAudioDurationAsync(audioFilePath);
                _logger.LogInformation("Audio duration: {Duration} seconds", duration);

                // Calculate chunk duration based on file size
                var fileSize = new FileInfo(audioFilePath).Length;
                var chunkDuration = (int)((MAX_AUDIO_FILE_SIZE / (double)fileSize) * duration * 0.9); // 90% of max to be safe
                
                // Ensure reasonable chunk size (at least 60 seconds, max 600 seconds)
                chunkDuration = Math.Max(60, Math.Min(chunkDuration, 600));
                
                _logger.LogInformation("Using chunk duration of {ChunkDuration} seconds", chunkDuration);

                cancellationToken.ThrowIfCancellationRequested();

                // Split audio into chunks
                var chunkFiles = await SplitAudioIntoChunksAsync(audioFilePath, tempChunkDirectory, chunkDuration);
                _logger.LogInformation("Created {Count} audio chunks", chunkFiles.Count);

                // Transcribe each chunk
                var transcripts = new string[chunkFiles.Count];
                var semaphore = new SemaphoreSlim(10); // Limit to (up to) 10 concurrent transcriptions
                var tasks = new List<Task>();

                for (int i = 0; i < chunkFiles.Count; i++)
                {
                    var index = i; // Capture index for closure
                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            _logger.LogInformation("Transcribing chunk {Current}/{Total}...", index + 1, chunkFiles.Count);
                            var chunkTranscript = await TranscribeSingleAudioFileAsync(chunkFiles[index], cancellationToken);
                            transcripts[index] = chunkTranscript;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                var combinedTranscript = string.Join(" ", transcripts);

                // Combine transcripts
                _logger.LogInformation("All chunks transcribed. Combined transcript length: {Length} characters", 
                    combinedTranscript.Length);

                return combinedTranscript;
            }
            finally
            {
                // Clean up temporary chunk files
                try
                {
                    if (Directory.Exists(tempChunkDirectory))
                    {
                        Directory.Delete(tempChunkDirectory, true);
                        _logger.LogInformation("Cleaned up temporary chunk directory");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary chunk directory: {Directory}", tempChunkDirectory);
                }
            }
        }

        private async Task<double> GetAudioDurationAsync(string audioFilePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start FFprobe process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFprobe failed: {error}");
            }

            if (!double.TryParse(output.Trim(), out var duration))
            {
                throw new InvalidOperationException($"Failed to parse audio duration: {output}");
            }

            return duration;
        }

        private async Task<List<string>> SplitAudioIntoChunksAsync(string audioFilePath, string outputDirectory, int chunkDurationSeconds)
        {
            var chunkFiles = new List<string>();
            var duration = await GetAudioDurationAsync(audioFilePath);
            var chunkCount = (int)Math.Ceiling(duration / chunkDurationSeconds);

            for (int i = 0; i < chunkCount; i++)
            {
                var startTime = i * chunkDurationSeconds;
                var chunkFile = Path.Combine(outputDirectory, $"chunk_{i:D3}.mp3");
                
                // Use FFmpeg to extract chunk with slight overlap
                var actualStart = Math.Max(0, startTime - (i > 0 ? CHUNK_OVERLAP_SECONDS : 0));
                var chunkDuration = chunkDurationSeconds + (i > 0 ? CHUNK_OVERLAP_SECONDS : 0);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{audioFilePath}\" -ss {actualStart} -t {chunkDuration} -acodec copy \"{chunkFile}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start FFmpeg process");
                }

                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"FFmpeg failed to create chunk {i}: {error}");
                }

                if (File.Exists(chunkFile))
                {
                    chunkFiles.Add(chunkFile);
                    _logger.LogInformation("Created chunk {Index}: {File} (start: {Start}s)", i, chunkFile, actualStart);
                }
            }

            return chunkFiles;
        }

        public async Task<string> SummarizeTranscriptAsync(string transcript, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting summarization. Transcript length: {Length} characters", transcript.Length);

                cancellationToken.ThrowIfCancellationRequested();

                var (client, deployment) = GetGPTClientAndDeployment();
                var chatClient = client.GetChatClient(deployment);

                // Comprehensive prompt designed to produce a 5-minute summary
                var systemPrompt = @"You are an expert audio summarizer specializing in creating concise, engaging audio summaries. Your summaries are designed to be read aloud and should sound natural when spoken.

Your task is to distill audio content into exactly 5 minutes worth of spoken content (approximately 750 words at 150 words per minute).

Guidelines:
1. CRITICAL: Stay within 750 words maximum. This is non-negotiable.
2. Capture ALL salient points, key insights, main arguments, and important takeaways
3. Maintain logical flow and narrative coherence
4. Use clear, conversational language appropriate for audio narration
5. Focus on substantive content; eliminate pleasantries, filler words, and tangential discussions
6. If the content is very long, prioritize the most impactful and actionable information
7. Structure your summary with a brief introduction, main content organized by themes, and a concise conclusion
8. Use transitions that work well in spoken form (e.g., 'Moving on to...', 'Another key point is...')

If the transcript is extremely long and contains more content than can fit in 750 words while preserving all key points, use a multi-pass distillation approach:
- First, identify the core themes and most critical insights
- Then, distill supporting details to their essence
- Finally, craft a coherent narrative that maximizes information density while maintaining clarity

Your summary should be ready to be converted directly to speech without any further editing.";

                var userPrompt = $@"Please summarize the following audio transcript into exactly 5 minutes of spoken content (approximately 750 words). Ensure all important points are captured while staying within the word limit.

Transcript:
{transcript}

Provide your summary below:";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 1.0f, // Lower temperature for more focused, consistent output
                };


                var response = await chatClient.CompleteChatAsync(messages, chatOptions);
                var summary = response.Value.Content[0].Text;

                _logger.LogInformation("Summarization completed. Summary length: {Length} characters, ~{Words} words",
                    summary.Length, summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                // If summary is still too long, perform a second pass
                var wordCount = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount > 800)
                {
                    _logger.LogWarning("Summary exceeded target length ({Words} words). Performing second pass compression.", wordCount);
                    summary = await CompressSummaryAsync(chatClient, summary);
                }

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing transcript");
                throw;
            }
        }

        private async Task<string> CompressSummaryAsync(ChatClient chatClient, string initialSummary)
        {
            var compressionPrompt = $@"The following summary is slightly too long for a 5-minute audio narration. Please compress it to exactly 750 words or fewer while preserving ALL key points and maintaining natural flow for speech.

Current summary:
{initialSummary}

Provide the compressed version:";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert editor specializing in compressing content while preserving meaning. Reduce the following text to 750 words maximum while keeping all critical information."),
                new UserChatMessage(compressionPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var compressedSummary = response.Value.Content[0].Text;
            _logger.LogInformation("Compression completed. New length: ~{Words} words",
                compressedSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            return compressedSummary;
        }

        public async Task<string> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting text-to-speech generation. Text length: {Length} characters", text.Length);

                cancellationToken.ThrowIfCancellationRequested();

                var (client, deployment) = GetTTSClientAndDeployment();
                var audioClient = client.GetAudioClient(deployment);

                // Get TTS configuration settings with override support
                var customSettings = GetCustomSettings();
                
                // Speed ratio: check custom settings, then configuration, then default to 1.0
                var speedRatio = customSettings?.TtsSpeedRatio 
                    ?? float.Parse(_configuration["AzureOpenAI:TextToSpeech:SpeedRatio"] ?? "1.0");
                
                // Response format: check custom settings, then configuration, then default to mp3
                var responseFormatString = customSettings?.TtsResponseFormat 
                    ?? _configuration["AzureOpenAI:TextToSpeech:ResponseFormat"] ?? "mp3";
                
                var responseFormat = responseFormatString.ToLowerInvariant() switch
                {
                    "mp3" => GeneratedSpeechFormat.Mp3,
                    "opus" => GeneratedSpeechFormat.Opus,
                    "aac" => GeneratedSpeechFormat.Aac,
                    "flac" => GeneratedSpeechFormat.Flac,
                    "wav" => GeneratedSpeechFormat.Wav,
                    "pcm" => GeneratedSpeechFormat.Pcm,
                    _ => GeneratedSpeechFormat.Mp3
                };

                _logger.LogInformation("Using TTS settings - SpeedRatio: {SpeedRatio}, Format: {Format}", 
                    speedRatio, responseFormatString);

                var generateOptions = new SpeechGenerationOptions
                {
                    ResponseFormat = responseFormat,
                    SpeedRatio = speedRatio
                };

                var result = await audioClient.GenerateSpeechAsync(text, GeneratedSpeechVoice.Alloy, generateOptions);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save audio to file
                using var audioStream = result.Value.ToStream();
                using var fileStream = File.Create(outputFilePath);
                await audioStream.CopyToAsync(fileStream);

                _logger.LogInformation("Text-to-speech generation completed. File saved to: {FilePath}", outputFilePath);

                return outputFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating speech from text");
                throw;
            }
        }
    }
}
