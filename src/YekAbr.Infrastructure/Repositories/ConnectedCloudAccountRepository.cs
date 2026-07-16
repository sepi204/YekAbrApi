using Microsoft.EntityFrameworkCore;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Persistence;

namespace YekAbr.Infrastructure.Repositories;

public sealed class ConnectedCloudAccountRepository : IConnectedCloudAccountRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ConnectedCloudAccountRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ConnectedCloudAccount entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.ConnectedCloudAccounts.AddAsync(entity, cancellationToken);
    }

    public Task<ConnectedCloudAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ConnectedCloudAccounts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectedCloudAccount>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConnectedCloudAccounts
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<ConnectedCloudAccount?> GetByUserIdAndProviderAccountIdAsync(
        string userId,
        CloudProviderType provider,
        string providerAccountId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ConnectedCloudAccounts.FirstOrDefaultAsync(
            x => x.UserId == userId
                 && x.Provider == provider
                 && x.ProviderAccountId == providerAccountId,
            cancellationToken);
    }

    public void Update(ConnectedCloudAccount entity)
    {
        _dbContext.ConnectedCloudAccounts.Update(entity);
    }

    public void Remove(ConnectedCloudAccount entity)
    {
        _dbContext.ConnectedCloudAccounts.Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
