using Quintessentia.Constants;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    /// <summary>
    /// Mock implementation of IAzureOpenAIService that returns pre-canned values
    /// instead of calling actual Azure OpenAI services. Useful for development and testing.
    /// </summary>
    public class MockAzureOpenAIService : IAzureOpenAIService
    {
        private readonly ILogger<MockAzureOpenAIService> _logger;
        private readonly IWebHostEnvironment _environment;

        public MockAzureOpenAIService(ILogger<MockAzureOpenAIService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
            _logger.LogInformation("MockAzureOpenAIService initialized - using pre-canned responses");
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[MOCK] Starting transcription for file: {FilePath}", audioFilePath);
            
            // Simulate processing delay
            await Task.Delay(AudioProcessingConstants.MockDelayMs, cancellationToken);

            var transcript = @"Welcome to this episode of Tech Insights. Today we're diving deep into the world of artificial intelligence and machine learning. 
            The landscape of AI has changed dramatically over the past few years. What once seemed like science fiction is now becoming an integral part of our daily lives. From voice assistants to recommendation systems, AI is everywhere.
            Let's start by discussing the fundamentals. Machine learning is a subset of artificial intelligence that focuses on building systems that can learn from data. Instead of being explicitly programmed to perform a task, these systems improve their performance through experience.
            There are three main types of machine learning: supervised learning, unsupervised learning, and reinforcement learning. Supervised learning involves training a model on labeled data, where the correct answers are known. Unsupervised learning works with unlabeled data, finding patterns and structures without predefined categories. Reinforcement learning is about training agents to make sequences of decisions by rewarding desired behaviors.
            Deep learning has revolutionized the field in recent years. By using neural networks with multiple layers, we can now tackle problems that were previously unsolvable. Image recognition, natural language processing, and speech synthesis have all seen tremendous improvements thanks to deep learning.
            However, with great power comes great responsibility. As AI systems become more prevalent, we must address important ethical considerations. Bias in training data can lead to biased AI systems. Privacy concerns arise when personal data is used to train models. And there's the ongoing debate about AI transparency and explainability.
            The future of AI is both exciting and challenging. We're seeing advances in areas like computer vision, where machines can understand and interpret visual information. Natural language processing continues to improve, enabling more natural human-computer interactions. And we're just beginning to explore the possibilities of AI in fields like healthcare, education, and scientific research.
            One particularly interesting development is the rise of large language models. These models, trained on vast amounts of text data, can generate human-like text, answer questions, and even write code. They represent a significant step forward in AI capabilities, though they also raise new questions about authenticity and misinformation.
            As we look ahead, it's clear that AI will continue to shape our world in profound ways. The key is to develop these technologies responsibly, ensuring they benefit humanity while minimizing potential harms. This requires collaboration between technologists, ethicists, policymakers, and the public.
            Thank you for joining us today. We hope this discussion has given you valuable insights into the current state and future potential of artificial intelligence. Until next time, keep exploring and stay curious about the technologies shaping our future.";

            _logger.LogInformation("[MOCK] Transcription completed. Length: {Length} characters", transcript.Length);
            
            return transcript;
        }

        public async Task<string> SummarizeTranscriptAsync(string transcript, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[MOCK] Starting summarization. Transcript length: {Length} characters", transcript.Length);
            
            // Simulate processing delay
            await Task.Delay(AudioProcessingConstants.MockDelayMs, cancellationToken);

            var summary = @"This episode of Tech Insights provides a comprehensive overview of artificial intelligence and machine learning, exploring how these technologies have evolved from science fiction concepts to integral parts of modern life.
            The discussion begins with fundamentals, explaining that machine learning is a subset of AI focused on building systems that learn from data rather than explicit programming. Three main types are covered: supervised learning with labeled data, unsupervised learning that finds patterns in unlabeled data, and reinforcement learning that trains agents through reward-based decision making.
            Deep learning emerges as a revolutionary force in the field. By leveraging neural networks with multiple layers, previously unsolvable problems in image recognition, natural language processing, and speech synthesis have seen tremendous breakthroughs. This advancement has opened new possibilities across numerous applications.
            However, the rapid advancement brings critical ethical considerations. The presentation addresses three major concerns: bias in AI systems stemming from biased training data, privacy issues when personal data trains models, and ongoing debates about AI transparency and explainability. These challenges require careful attention as AI becomes more pervasive.
            Looking toward the future, several exciting developments are highlighted. Computer vision advances enable machines to understand visual information more effectively. Natural language processing improvements facilitate more natural human-computer interactions. The potential applications in healthcare, education, and scientific research are just beginning to be explored.
            Large language models represent a particularly significant development. Trained on vast text datasets, these models can generate human-like text, answer questions, and even write code. While they mark substantial progress in AI capabilities, they also introduce new concerns about authenticity and the spread of misinformation.
            The episode emphasizes that AI will continue profoundly shaping our world. The critical challenge lies in responsible development—ensuring technologies benefit humanity while minimizing potential harms. This requires collaborative efforts among technologists, ethicists, policymakers, and the broader public.
            Key takeaways include understanding AI's fundamental concepts, recognizing both its transformative potential and inherent challenges, and appreciating the importance of ethical considerations in development and deployment. The technology's future depends on balancing innovation with responsibility.
            The presentation concludes by encouraging continued exploration and curiosity about AI technologies. As these systems become more sophisticated and integrated into daily life, staying informed about their capabilities, limitations, and implications becomes increasingly important for everyone.
            This balanced perspective helps listeners understand not just what AI can do, but also what responsibilities come with its development and use. The future of AI is collaborative, requiring input from diverse stakeholders to ensure technology serves humanity's best interests while addressing legitimate concerns about privacy, bias, and transparency.";

            _logger.LogInformation("[MOCK] Summarization completed. Summary length: {Length} characters, ~{Words} words",
                summary.Length, summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            
            return summary;
        }

        public async Task<string> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[MOCK] Starting text-to-speech generation. Text length: {Length} characters", text.Length);
            
            // Simulate processing delay
            await Task.Delay(AudioProcessingConstants.MockDelayMs, cancellationToken);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Copy the sample MP3 file to the output location
            var sampleMp3Path = Path.Combine(_environment.WebRootPath, "sample-audio.mp3");
            
            if (File.Exists(sampleMp3Path))
            {
                File.Copy(sampleMp3Path, outputFilePath, overwrite: true);
                _logger.LogInformation("[MOCK] Text-to-speech generation completed. Sample audio copied to: {FilePath}", outputFilePath);
            }
            else
            {
                // If sample file doesn't exist, create a minimal valid MP3 file
                _logger.LogWarning("[MOCK] Sample MP3 not found at {SamplePath}, creating minimal MP3", sampleMp3Path);
                await CreateMinimalMp3FileAsync(outputFilePath, cancellationToken);
            }

            return outputFilePath;
        }

        private async Task CreateMinimalMp3FileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            // Create a minimal valid MP3 file (silent audio, about 1 second)
            // MP3 header for a very short silent file
            byte[] minimalMp3 = new byte[]
            {
                // ID3v2 header
                0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                // MP3 frame header (MPEG-1 Layer 3, 128kbps, 44.1kHz, mono)
                0xFF, 0xFB, 0x90, 0x00,
                // Minimal frame data (zeros for silence)
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            await File.WriteAllBytesAsync(filePath, minimalMp3, cancellationToken);
            _logger.LogInformation("[MOCK] Created minimal MP3 file at: {FilePath}", filePath);
        }
    }
}
