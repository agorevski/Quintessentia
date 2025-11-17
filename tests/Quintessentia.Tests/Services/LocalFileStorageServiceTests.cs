using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Services;
using Xunit;

namespace Quintessentia.Tests.Services
{
    public class LocalFileStorageServiceTests : IDisposable
    {
        private readonly Mock<ILogger<LocalFileStorageService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly LocalFileStorageService _service;
        private readonly string _testBasePath;

        public LocalFileStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<LocalFileStorageService>>();
            _mockConfiguration = new Mock<IConfiguration>();

            // Setup unique test directory
            _testBasePath = Path.Combine(Path.GetTempPath(), "LocalFileStorageServiceTests", Guid.NewGuid().ToString());
            
            _mockConfiguration.Setup(c => c["LocalStorage:BasePath"]).Returns(_testBasePath);
            _mockConfiguration.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns("episodes");
            _mockConfiguration.Setup(c => c["AzureStorage:Containers:Transcripts"]).Returns("transcripts");
            _mockConfiguration.Setup(c => c["AzureStorage:Containers:Summaries"]).Returns("summaries");

            _service = new LocalFileStorageService(_mockConfiguration.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, recursive: true);
            }
        }

        [Fact]
        public void Constructor_CreatesBaseDirectory()
        {
            // Assert
            Directory.Exists(_testBasePath).Should().BeTrue();
        }

        [Fact]
        public void Constructor_CreatesContainerDirectories()
        {
            // Assert
            Directory.Exists(Path.Combine(_testBasePath, "episodes")).Should().BeTrue();
            Directory.Exists(Path.Combine(_testBasePath, "transcripts")).Should().BeTrue();
            Directory.Exists(Path.Combine(_testBasePath, "summaries")).Should().BeTrue();
        }

        [Fact]
        public async Task UploadStreamAsync_CreatesFile()
        {
            // Arrange
            var containerName = "test-container";
            var blobName = "test.txt";
            var content = "Hello, World!";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

            // Act
            var result = await _service.UploadStreamAsync(containerName, blobName, stream);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().StartWith("file://");
            
            var filePath = Path.Combine(_testBasePath, containerName, blobName);
            File.Exists(filePath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(filePath);
            fileContent.Should().Be(content);
        }

        [Fact]
        public async Task UploadFileAsync_UploadsFileSuccessfully()
        {
            // Arrange
            var sourceFile = Path.Combine(_testBasePath, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "Test content");
            
            var containerName = "uploads";
            var blobName = "uploaded.txt";

            // Act
            var result = await _service.UploadFileAsync(containerName, blobName, sourceFile);

            // Assert
            result.Should().NotBeNullOrEmpty();
            var uploadedFile = Path.Combine(_testBasePath, containerName, blobName);
            File.Exists(uploadedFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(uploadedFile);
            content.Should().Be("Test content");
        }

        [Fact]
        public async Task DownloadToStreamAsync_DownloadsFileSuccessfully()
        {
            // Arrange
            var containerName = "downloads";
            var blobName = "download.txt";
            var testContent = "Download test";
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, blobName), testContent);

            using var targetStream = new MemoryStream();

            // Act
            await _service.DownloadToStreamAsync(containerName, blobName, targetStream);

            // Assert
            targetStream.Position.Should().Be(0);
            targetStream.Length.Should().BeGreaterThan(0);
            
            var content = System.Text.Encoding.UTF8.GetString(targetStream.ToArray());
            content.Should().Be(testContent);
        }

        [Fact]
        public async Task DownloadToStreamAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
        {
            // Arrange
            var containerName = "missing";
            var blobName = "notfound.txt";
            using var targetStream = new MemoryStream();

            // Act
            var act = async () => await _service.DownloadToStreamAsync(containerName, blobName, targetStream);

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task DownloadToFileAsync_DownloadsFileSuccessfully()
        {
            // Arrange
            var containerName = "files";
            var blobName = "source.txt";
            var testContent = "File download test";
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, blobName), testContent);

            var targetPath = Path.Combine(_testBasePath, "downloaded.txt");

            // Act
            await _service.DownloadToFileAsync(containerName, blobName, targetPath);

            // Assert
            File.Exists(targetPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(targetPath);
            content.Should().Be(testContent);
        }

        [Fact]
        public async Task DownloadToFileAsync_ThrowsFileNotFoundException_WhenSourceDoesNotExist()
        {
            // Arrange
            var containerName = "missing";
            var blobName = "notfound.txt";
            var targetPath = Path.Combine(_testBasePath, "target.txt");

            // Act
            var act = async () => await _service.DownloadToFileAsync(containerName, blobName, targetPath);

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task ExistsAsync_ReturnsTrue_WhenFileExists()
        {
            // Arrange
            var containerName = "check";
            var blobName = "exists.txt";
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, blobName), "exists");

            // Act
            var result = await _service.ExistsAsync(containerName, blobName);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
        {
            // Arrange
            var containerName = "check";
            var blobName = "notexists.txt";

            // Act
            var result = await _service.ExistsAsync(containerName, blobName);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_DeletesFileSuccessfully()
        {
            // Arrange
            var containerName = "delete";
            var blobName = "todelete.txt";
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, blobName);
            await File.WriteAllTextAsync(filePath, "delete me");

            // Act
            await _service.DeleteAsync(containerName, blobName);

            // Assert
            File.Exists(filePath).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_DoesNotThrow_WhenFileDoesNotExist()
        {
            // Arrange
            var containerName = "delete";
            var blobName = "notexists.txt";

            // Act
            var act = async () => await _service.DeleteAsync(containerName, blobName);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GetBlobSizeAsync_ReturnsCorrectSize()
        {
            // Arrange
            var containerName = "size";
            var blobName = "file.txt";
            var content = "12345"; // 5 bytes
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, blobName), content);

            // Act
            var size = await _service.GetBlobSizeAsync(containerName, blobName);

            // Assert
            size.Should().Be(5);
        }

        [Fact]
        public async Task GetBlobSizeAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
        {
            // Arrange
            var containerName = "size";
            var blobName = "notfound.txt";

            // Act
            var act = async () => await _service.GetBlobSizeAsync(containerName, blobName);

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task UploadStreamAsync_CreatesDirectoryIfNeeded()
        {
            // Arrange
            var containerName = "newcontainer";
            var blobName = "file.txt";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));

            // Act
            await _service.UploadStreamAsync(containerName, blobName, stream);

            // Assert
            Directory.Exists(Path.Combine(_testBasePath, containerName)).Should().BeTrue();
        }

        [Fact]
        public async Task DownloadToFileAsync_CreatesTargetDirectoryIfNeeded()
        {
            // Arrange
            var containerName = "source";
            var blobName = "file.txt";
            
            var directory = Path.Combine(_testBasePath, containerName);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, blobName), "test");

            var targetPath = Path.Combine(_testBasePath, "newdir", "subdir", "target.txt");

            // Act
            await _service.DownloadToFileAsync(containerName, blobName, targetPath);

            // Assert
            File.Exists(targetPath).Should().BeTrue();
        }

        [Fact]
        public async Task UploadStreamAsync_ResetsStreamPosition()
        {
            // Arrange
            var containerName = "test";
            var blobName = "stream.txt";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));
            stream.Position = 2; // Set to middle

            // Act
            await _service.UploadStreamAsync(containerName, blobName, stream);

            // Assert
            var filePath = Path.Combine(_testBasePath, containerName, blobName);
            var content = await File.ReadAllTextAsync(filePath);
            content.Should().Be("test", "entire stream content should be written");
        }
    }
}
