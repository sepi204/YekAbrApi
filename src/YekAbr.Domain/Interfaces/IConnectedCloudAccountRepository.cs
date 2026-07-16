using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;

namespace YekAbr.Domain.Interfaces;

public interface IConnectedCloudAccountRepository
{
    Task AddAsync(ConnectedCloudAccount entity, CancellationToken cancellationToken = default);
    Task<ConnectedCloudAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectedCloudAccount>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<ConnectedCloudAccount?> GetByUserIdAndProviderAccountIdAsync(
        string userId,
        CloudProviderType provider,
        string providerAccountId,
        CancellationToken cancellationToken = default);
    void Update(ConnectedCloudAccount entity);
    void Remove(ConnectedCloudAccount entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
