using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class StorageConfiguration : IStorageConfiguration
    {
        private readonly IConfiguration _configuration;

        public StorageConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetContainerName(string containerType)
        {
            return _configuration[$"AzureStorage:Containers:{containerType}"] ?? containerType.ToLower();
        }
    }
}
