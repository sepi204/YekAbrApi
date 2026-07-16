namespace YekAbr.Infrastructure.Cloud.Mega;

/// <summary>
/// MEGA uses email/password (optional MFA) via MegaApiClient — not OAuth client credentials.
/// </summary>
public sealed class MegaOptions
{
    public const string SectionName = "Mega";

    /// <summary>
    /// Optional HTTP/API timeout applied to MegaApiClient operations where supported.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;
}
