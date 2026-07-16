using YekAbr.Domain.Entities;

namespace YekAbr.Domain.Interfaces;

public interface ICloudTransferJobRepository
{
    Task AddAsync(CloudTransferJob entity, CancellationToken cancellationToken = default);
    Task<CloudTransferJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CloudTransferJob>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    void Update(CloudTransferJob entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
