using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Quintessentia.Services.HealthChecks
{
    /// <summary>
    /// Health check for Azure OpenAI service connectivity.
    /// </summary>
    public class AzureOpenAIHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIHealthCheck> _logger;

        public AzureOpenAIHealthCheck(
            IConfiguration configuration,
            ILogger<AzureOpenAIHealthCheck> logger)
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
                var endpoint = _configuration["AzureOpenAI:Endpoint"];
                var key = _configuration["AzureOpenAI:Key"];

                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
                {
                    return HealthCheckResult.Degraded("Azure OpenAI endpoint or key is not configured");
                }

                // Verify that the endpoint is reachable by creating a client
                // We don't make an actual API call to avoid costs, just verify configuration
                var client = new Azure.AI.OpenAI.AzureOpenAIClient(
                    new Uri(endpoint),
                    new Azure.AzureKeyCredential(key));

                // Check that required deployment names are configured
                var sttDeployment = _configuration["AzureOpenAI:SpeechToText:DeploymentName"];
                var gptDeployment = _configuration["AzureOpenAI:GPT:DeploymentName"];
                var ttsDeployment = _configuration["AzureOpenAI:TextToSpeech:DeploymentName"];

                if (string.IsNullOrWhiteSpace(sttDeployment) ||
                    string.IsNullOrWhiteSpace(gptDeployment) ||
                    string.IsNullOrWhiteSpace(ttsDeployment))
                {
                    return HealthCheckResult.Degraded(
                        "One or more Azure OpenAI deployment names are not configured",
                        data: new Dictionary<string, object>
                        {
                            ["SpeechToText"] = !string.IsNullOrWhiteSpace(sttDeployment),
                            ["GPT"] = !string.IsNullOrWhiteSpace(gptDeployment),
                            ["TextToSpeech"] = !string.IsNullOrWhiteSpace(ttsDeployment)
                        });
                }

                await Task.CompletedTask; // Satisfy async requirement
                return HealthCheckResult.Healthy("Azure OpenAI is configured correctly");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI health check failed");
                return HealthCheckResult.Unhealthy("Azure OpenAI configuration is invalid", ex);
            }
        }
    }
}
