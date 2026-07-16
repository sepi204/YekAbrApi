using YekAbr.Domain.Enums;
using YekAbr.Services.Common.Exceptions;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Cloud;

public sealed class CloudProviderClientFactory : ICloudProviderClientFactory
{
    private readonly IReadOnlyDictionary<CloudProviderType, ICloudProviderClient> _providers;

    public CloudProviderClientFactory(IEnumerable<ICloudProviderClient> providers)
    {
        var providerList = providers.ToList();

        var duplicates = providerList
            .GroupBy(x => x.ProviderType)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate cloud provider registrations detected: {string.Join(", ", duplicates)}.");
        }

        _providers = providerList.ToDictionary(x => x.ProviderType);
    }

    public ICloudProviderClient GetProvider(CloudProviderType providerType)
    {
        if (!_providers.TryGetValue(providerType, out var provider))
        {
            throw new UnsupportedCloudProviderException(providerType);
        }

        return provider;
    }

    public ICloudFileProviderClient GetFileProvider(CloudProviderType providerType)
    {
        var provider = GetProvider(providerType);
        if (provider is ICloudFileProviderClient fileProvider)
        {
            return fileProvider;
        }

        throw new UnsupportedCloudProviderException(providerType);
    }

    public bool IsSupported(CloudProviderType providerType)
    {
        return _providers.ContainsKey(providerType);
    }
}
