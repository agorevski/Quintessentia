using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Quintessentia.Services.HealthChecks
{
    /// <summary>
    /// Health check for Azure Blob Storage connectivity.
    /// </summary>
    public class AzureBlobStorageHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureBlobStorageHealthCheck> _logger;

        public AzureBlobStorageHealthCheck(
            IConfiguration configuration,
            ILogger<AzureBlobStorageHealthCheck> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionString = _configuration["AzureStorageConnectionString"];

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return HealthCheckResult.Degraded("Azure Storage connection string is not configured");
                }

                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
                var properties = await blobServiceClient.GetPropertiesAsync(cancellationToken);

                return HealthCheckResult.Healthy("Azure Blob Storage is accessible");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure Blob Storage health check failed");
                return HealthCheckResult.Unhealthy("Azure Blob Storage is not accessible", ex);
            }
        }
    }
}
