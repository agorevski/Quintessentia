namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for generating cache keys from URLs
    /// </summary>
    public interface ICacheKeyService
    {
        /// <summary>
        /// Generates a cache key from a URL using SHA-256 hashing
        /// </summary>
        /// <param name="url">The URL to generate a cache key for</param>
        /// <returns>A 32-character lowercase hexadecimal cache key</returns>
        string GenerateFromUrl(string url);
    }
}
