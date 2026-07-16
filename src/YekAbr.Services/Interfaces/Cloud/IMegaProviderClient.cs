using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// MEGA provider: credential-based connect + file operations.
/// Durable credential material is an encrypted AuthInfos payload, not OAuth tokens.
/// </summary>
public interface IMegaProviderClient : ICloudFileProviderClient
{
    /// <summary>
    /// Authenticates with MEGA email/password (optional MFA) and returns
    /// durable AuthInfos JSON plus account/root metadata for persistence.
    /// </summary>
    Task<MegaConnectionMaterial> CreateConnectionMaterialAsync(
        string email,
        string password,
        string? mfaKey,
        CancellationToken cancellationToken = default);
}
