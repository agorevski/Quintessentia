using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly Dictionary<string, BlobContainerClient> _containerClients;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly string[] _containerNames;
        private bool _initialized;

        public AzureBlobStorageService(
            IConfiguration configuration,
            ILogger<AzureBlobStorageService> logger)
        {
            var connectionString = configuration["AzureStorageConnectionString"];
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Azure Storage connection string is not configured.");
            }

            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger = logger;
            _containerClients = new Dictionary<string, BlobContainerClient>();
            
            // Store container names for lazy initialization
            _containerNames = new[]
            {
                configuration["AzureStorage:Containers:Episodes"] ?? "episodes",
                configuration["AzureStorage:Containers:Transcripts"] ?? "transcripts",
                configuration["AzureStorage:Containers:Summaries"] ?? "summaries"
            };
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                foreach (var containerName in _containerNames)
                {
                    try
                    {
                        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                        _containerClients[containerName] = containerClient;
                        _logger.LogInformation("Initialized blob container: {ContainerName}", containerName);
                    }
                    catch (RequestFailedException ex)
                    {
                        _logger.LogError(ex, "Azure storage error initializing container: {ContainerName}", containerName);
                        throw;
                    }
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);
            
            if (_containerClients.TryGetValue(containerName, out var client))
            {
                return client;
            }

            // Fallback: create client on-demand if not in dictionary
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            _containerClients[containerName] = containerClient;
            return containerClient;
        }

        public async Task<string> UploadStreamAsync(string containerName, string blobName, Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                stream.Position = 0; // Reset stream position
                await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

                _logger.LogInformation("Uploaded blob: {ContainerName}/{BlobName}", containerName, blobName);
                return blobClient.Uri.ToString();
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error uploading blob: {ContainerName}/{BlobName}", containerName, blobName);
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
                _logger.LogError(ex, "IO error reading file to upload: {LocalPath} -> {ContainerName}/{BlobName}", 
                    localPath, containerName, blobName);
                throw;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error uploading file: {LocalPath} -> {ContainerName}/{BlobName}", 
                    localPath, containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToStreamAsync(string containerName, string blobName, Stream targetStream, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DownloadToAsync(targetStream, cancellationToken);
                targetStream.Position = 0; // Reset stream position for reading

                _logger.LogInformation("Downloaded blob to stream: {ContainerName}/{BlobName}", containerName, blobName);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error downloading blob to stream: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }

        public async Task DownloadToFileAsync(string containerName, string blobName, string localPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await blobClient.DownloadToAsync(localPath, cancellationToken);

                _logger.LogInformation("Downloaded blob to file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading blob to file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
                throw;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error downloading blob to file: {ContainerName}/{BlobName} -> {LocalPath}", 
                    containerName, blobName, localPath);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.ExistsAsync(cancellationToken);
                return response.Value;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error checking blob existence: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                return false;
            }
        }

        public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

                _logger.LogInformation("Deleted blob: {ContainerName}/{BlobName}", containerName, blobName);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error deleting blob: {ContainerName}/{BlobName}", containerName, blobName);
                throw;
            }
        }

        public async Task<long> GetBlobSizeAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                return properties.Value.ContentLength;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error getting blob size: {ContainerName}/{BlobName}", 
                    containerName, blobName);
                throw;
            }
        }
    }
}
