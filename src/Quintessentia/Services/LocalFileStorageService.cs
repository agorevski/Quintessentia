using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class LocalFileStorageService : IStorageService
    {
        private readonly string _basePath;
        private readonly ILogger<LocalFileStorageService> _logger;
        private readonly IConfiguration _configuration;

        public LocalFileStorageService(
            IConfiguration configuration,
            ILogger<LocalFileStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _basePath = configuration["LocalStorage:BasePath"] ?? "LocalStorageData";
            
            // Ensure base directory exists
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Created local storage base directory: {BasePath}", _basePath);
            }

            // Initialize container directories
            InitializeContainers(configuration);
        }

        private void InitializeContainers(IConfiguration configuration)
        {
            var containerNames = new[]
            {
                configuration["AzureStorage:Containers:Episodes"] ?? "episodes",
                configuration["AzureStorage:Containers:Transcripts"] ?? "transcripts",
                configuration["AzureStorage:Containers:Summaries"] ?? "summaries"
            };

            foreach (var containerName in containerNames)
            {
                var containerPath = Path.Combine(_basePath, containerName);
                if (!Directory.Exists(containerPath))
                {
                    Directory.CreateDirectory(containerPath);
                    _logger.LogInformation("Initialized local storage container: {ContainerName}", containerName);
                }
            }
        }

        private string GetFilePath(string containerName, string blobName)
        {
            return Path.Combine(_basePath, containerName, blobName);
        }

        public async Task<string> UploadStreamAsync(string containerName, string blobName, Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePath(containerName, blobName);
                var directory = Path.GetDirectoryName(filePath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                stream.Position = 0; // Reset stream position
                
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await stream.CopyToAsync(fileStream, cancellationToken);

                _logger.LogInformation("Uploaded file: {ContainerName}/{BlobName}", containerName, blobName);
                // Return file:// URI for local files
                return new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error uploading file: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied uploading file: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
        }

        public async Task<string> UploadFileAsync(string containerName, string blobName, string localPath, CancellationToken cancellationToken = default)
        {
            try
            {
                using var fileStream = File.OpenRead(localPath);
                return await UploadStreamAsync(containerName, blobName, fileStream, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error uploading file: {LocalPath} -> {ContainerName}/{BlobName}", 
                    localPath, containerName, blobName);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied uploading file: {LocalPath} -> {ContainerName}/{BlobName}", 
                    localPath, containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToStreamAsync(string containerName, string blobName, Stream targetStream, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePath(containerName, blobName);
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {containerName}/{blobName}");
                }

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await fileStream.CopyToAsync(targetStream, cancellationToken);
                targetStream.Position = 0; // Reset stream position for reading

                _logger.LogInformation("Downloaded file to stream: {ContainerName}/{BlobName}", containerName, blobName);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading file to stream: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToFileAsync(string containerName, string blobName, string localPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var sourcePath = GetFilePath(containerName, blobName);
                
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"File not found: {containerName}/{blobName}");
                }

                // Ensure target directory exists
                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Copy file asynchronously
                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                using var destinationStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);

                _logger.LogInformation("Downloaded file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied downloading file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
                throw;
            }
        }

        public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePath(containerName, blobName);
                return Task.FromResult(File.Exists(filePath));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error checking file existence: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                return Task.FromResult(false);
            }
        }

        public Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePath(containerName, blobName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted file: {ContainerName}/{BlobName}", containerName, blobName);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error deleting file: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied deleting file: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
            
            return Task.CompletedTask;
        }

        public Task<long> GetBlobSizeAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetFilePath(containerName, blobName);
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {containerName}/{blobName}");
                }

                var fileInfo = new FileInfo(filePath);
                return Task.FromResult(fileInfo.Length);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error getting file size: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }
    }
}
