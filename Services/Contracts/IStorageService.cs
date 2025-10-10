namespace Quintessentia.Services.Contracts
{
    public interface IStorageService
    {
        /// <summary>
        /// Uploads a stream to storage
        /// </summary>
        Task<string> UploadStreamAsync(string containerName, string blobName, Stream stream);

        /// <summary>
        /// Downloads a blob to a stream
        /// </summary>
        Task DownloadToStreamAsync(string containerName, string blobName, Stream targetStream);

        /// <summary>
        /// Downloads a blob to a local file
        /// </summary>
        Task DownloadToFileAsync(string containerName, string blobName, string localPath);

        /// <summary>
        /// Checks if a blob exists
        /// </summary>
        Task<bool> ExistsAsync(string containerName, string blobName);

        /// <summary>
        /// Deletes a blob
        /// </summary>
        Task DeleteAsync(string containerName, string blobName);

        /// <summary>
        /// Gets the size of a blob in bytes
        /// </summary>
        Task<long> GetBlobSizeAsync(string containerName, string blobName);

        /// <summary>
        /// Uploads a file from local path to storage
        /// </summary>
        Task<string> UploadFileAsync(string containerName, string blobName, string localPath);
    }
}
