using Microsoft.EntityFrameworkCore;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Persistence;

namespace YekAbr.Infrastructure.Repositories;

public sealed class CloudTransferJobRepository : ICloudTransferJobRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CloudTransferJobRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CloudTransferJob entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.CloudTransferJobs.AddAsync(entity, cancellationToken);
    }

    public Task<CloudTransferJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.CloudTransferJobs
            .Include(x => x.SourceConnectedCloudAccount)
            .Include(x => x.DestinationConnectedCloudAccount)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CloudTransferJob>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CloudTransferJobs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Update(CloudTransferJob entity)
    {
        _dbContext.CloudTransferJobs.Update(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
