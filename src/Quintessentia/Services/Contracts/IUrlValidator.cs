namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for validating URLs with security checks.
    /// </summary>
    public interface IUrlValidator
    {
        /// <summary>
        /// Validates a URL for security and format requirements.
        /// </summary>
        /// <param name="url">The URL to validate.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if the URL is valid and safe; false otherwise.</returns>
        bool ValidateUrl(string url, out string? errorMessage);

        /// <summary>
        /// Checks if a URL is valid HTTP/HTTPS format.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True if the URL is a valid HTTP/HTTPS URL.</returns>
        bool IsValidHttpUrl(string url);
    }
}
