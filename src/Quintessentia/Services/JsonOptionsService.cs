using System.Text.Json;

namespace Quintessentia.Services
{
    /// <summary>
    /// Provides shared JsonSerializerOptions for consistent JSON serialization across the application.
    /// </summary>
    public class JsonOptionsService
    {
        /// <summary>
        /// Gets the shared JsonSerializerOptions instance with standard configuration.
        /// </summary>
        public JsonSerializerOptions Options { get; }

        public JsonOptionsService()
        {
            Options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
    }
}
