namespace YekAbr.Services.Interfaces.Profile;

public interface IPublicUrlBuilder
{
    /// <summary>
    /// Converts a relative web path (e.g. /uploads/profiles/a.jpg) to an absolute URL.
    /// Returns null when the input is null/empty.
    /// </summary>
    string? ToAbsoluteUrl(string? relativePath);
}
