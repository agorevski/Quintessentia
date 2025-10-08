using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Quintessentia.Services
{
    public class AzureBlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly Dictionary<string, BlobContainerClient> _containerClients;

        public AzureBlobStorageService(
            IConfiguration configuration,
            ILogger<AzureBlobStorageService> logger)
        {
            var connectionString = configuration["AzureStorageConnectionString"];
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Azure Storage connection string is not configured.");
            }

            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger = logger;
            _containerClients = new Dictionary<string, BlobContainerClient>();

            // Initialize containers
            InitializeContainersAsync(configuration).GetAwaiter().GetResult();
        }

        private async Task InitializeContainersAsync(IConfiguration configuration)
        {
            var containerNames = new[]
            {
                configuration["AzureStorage:Containers:Episodes"] ?? "episodes",
                configuration["AzureStorage:Containers:Transcripts"] ?? "transcripts",
                configuration["AzureStorage:Containers:Summaries"] ?? "summaries"
            };

            foreach (var containerName in containerNames)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                    _containerClients[containerName] = containerClient;
                    _logger.LogInformation("Initialized blob container: {ContainerName}", containerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing container: {ContainerName}", containerName);
                    throw;
                }
            }
        }

        private BlobContainerClient GetContainerClient(string containerName)
        {
            if (_containerClients.TryGetValue(containerName, out var client))
            {
                return client;
            }

            // Fallback: create client on-demand if not in dictionary
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            _containerClients[containerName] = containerClient;
            return containerClient;
        }

        public async Task<string> UploadStreamAsync(string containerName, string blobName, Stream stream)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                stream.Position = 0; // Reset stream position
                await blobClient.UploadAsync(stream, overwrite: true);

                _logger.LogInformation("Uploaded blob: {ContainerName}/{BlobName}", containerName, blobName);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading blob: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
        }

        public async Task<string> UploadFileAsync(string containerName, string blobName, string localPath)
        {
            try
            {
                using var fileStream = File.OpenRead(localPath);
                return await UploadStreamAsync(containerName, blobName, fileStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to blob: {LocalPath} -> {ContainerName}/{BlobName}", 
                    localPath, containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToStreamAsync(string containerName, string blobName, Stream targetStream)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DownloadToAsync(targetStream);
                targetStream.Position = 0; // Reset stream position for reading

                _logger.LogInformation("Downloaded blob to stream: {ContainerName}/{BlobName}", containerName, blobName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob to stream: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToFileAsync(string containerName, string blobName, string localPath)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await blobClient.DownloadToAsync(localPath);

                _logger.LogInformation("Downloaded blob to file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob to file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                return await blobClient.ExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking blob existence: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                return false;
            }
        }

        public async Task DeleteAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync();

                _logger.LogInformation("Deleted blob: {ContainerName}/{BlobName}", containerName, blobName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
        }

        public async Task<long> GetBlobSizeAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var properties = await blobClient.GetPropertiesAsync();
                return properties.Value.ContentLength;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blob size: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }
    }
}
