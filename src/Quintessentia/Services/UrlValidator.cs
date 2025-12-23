using Quintessentia.Services.Contracts;
using System.Net;

namespace Quintessentia.Services
{
    /// <summary>
    /// Service for validating URLs with security checks against SSRF and other attacks.
    /// </summary>
    public class UrlValidator : IUrlValidator
    {
        private readonly ILogger<UrlValidator> _logger;
        private readonly HashSet<string> _blockedHosts;
        private readonly HashSet<string> _allowedSchemes;

        public UrlValidator(ILogger<UrlValidator> logger)
        {
            _logger = logger;
            
            // Blocked hosts to prevent SSRF attacks
            _blockedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "localhost",
                "127.0.0.1",
                "::1",
                "[::1]",
                "0.0.0.0",
                "169.254.169.254", // AWS/Azure metadata service
                "metadata.google.internal", // GCP metadata service
            };

            _allowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Uri.UriSchemeHttp,
                Uri.UriSchemeHttps
            };
        }

        /// <inheritdoc/>
        public bool ValidateUrl(string url, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                errorMessage = "URL cannot be empty.";
                return false;
            }

            // Validate URL format
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                errorMessage = "Invalid URL format.";
                return false;
            }

            // Check scheme
            if (!_allowedSchemes.Contains(uri.Scheme))
            {
                errorMessage = "Only HTTP and HTTPS URLs are allowed.";
                return false;
            }

            // Check for blocked hosts (SSRF protection)
            if (_blockedHosts.Contains(uri.Host))
            {
                _logger.LogWarning("Blocked URL request to internal host: {Host}", uri.Host);
                errorMessage = "The requested URL is not allowed.";
                return false;
            }

            // Check for private IP ranges
            if (IsPrivateIpAddress(uri.Host))
            {
                _logger.LogWarning("Blocked URL request to private IP: {Host}", uri.Host);
                errorMessage = "The requested URL is not allowed.";
                return false;
            }

            // Check for suspicious patterns
            if (uri.Host.Contains("internal") || uri.Host.Contains("metadata"))
            {
                _logger.LogWarning("Blocked URL request with suspicious host pattern: {Host}", uri.Host);
                errorMessage = "The requested URL is not allowed.";
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool IsValidHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   _allowedSchemes.Contains(uri.Scheme);
        }

        private bool IsPrivateIpAddress(string host)
        {
            if (!IPAddress.TryParse(host, out var ipAddress))
                return false;

            var bytes = ipAddress.GetAddressBytes();

            // IPv4 private ranges
            if (bytes.Length == 4)
            {
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;

                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;

                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;

                // Link-local 169.254.0.0/16
                if (bytes[0] == 169 && bytes[1] == 254)
                    return true;
            }

            // IPv6 private ranges (simplified check for link-local and loopback)
            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal)
                return true;

            return false;
        }
    }
}
