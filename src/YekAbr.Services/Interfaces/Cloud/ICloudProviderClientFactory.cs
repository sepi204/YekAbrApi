using YekAbr.Domain.Enums;

namespace YekAbr.Services.Interfaces.Cloud;

public interface ICloudProviderClientFactory
{
    ICloudProviderClient GetProvider(CloudProviderType providerType);

    ICloudFileProviderClient GetFileProvider(CloudProviderType providerType);

    bool IsSupported(CloudProviderType providerType);
}
