namespace YekAbr.Services.Interfaces.Cloud;

public interface ICloudOAuthStateStore
{
    Task StoreAsync(string state, string userId, TimeSpan lifetime, CancellationToken cancellationToken = default);

    Task<string?> ConsumeAsync(string state, CancellationToken cancellationToken = default);
}
