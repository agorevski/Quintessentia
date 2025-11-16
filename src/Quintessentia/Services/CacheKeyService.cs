using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class CacheKeyService : ICacheKeyService
    {
        private readonly ILogger<CacheKeyService> _logger;

        public CacheKeyService(ILogger<CacheKeyService> logger)
        {
            _logger = logger;
        }

        public string GenerateFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));
            }

            // If it looks like a URL, generate a hash-based cache key
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                
                // Use first 32 characters for a reasonable filename
                var cacheKey = hash.Substring(0, 32);
                
                _logger.LogDebug("Generated cache key {CacheKey} for URL: {Url}", cacheKey, url);
                return cacheKey;
            }

            // Otherwise, use as-is (for backwards compatibility or direct cache keys)
            _logger.LogDebug("Using URL as-is for cache key: {Url}", url);
            return url;
        }
    }
}
