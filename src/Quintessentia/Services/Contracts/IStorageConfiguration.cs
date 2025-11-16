namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for reading storage-related configuration
    /// </summary>
    public interface IStorageConfiguration
    {
        /// <summary>
        /// Gets the container name for a specific storage type
        /// </summary>
        /// <param name="containerType">The type of container (e.g., "Episodes", "Summaries", "Transcripts")</param>
        /// <returns>The configured container name, or lowercase default if not configured</returns>
        string GetContainerName(string containerType);
    }
}
