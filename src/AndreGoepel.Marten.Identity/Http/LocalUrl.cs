namespace AndreGoepel.Marten.Identity.Http;

/// <summary>
/// Guards post-authentication redirects against open-redirect attacks
/// (CWE-601). Only same-site, absolute-path URLs are treated as safe;
/// absolute URLs (<c>https://evil.example</c>), protocol-relative URLs
/// (<c>//evil.example</c>), and backslash tricks (<c>/\evil.example</c>)
/// are rejected.
/// </summary>
public static class LocalUrl
{
    /// <summary>
    /// Returns <c>true</c> only for a non-empty, rooted path that cannot be
    /// interpreted by a browser as a navigation to another origin.
    /// </summary>
    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Must be an absolute path beginning with a single '/'.
        if (url[0] != '/')
            return false;

        // Reject "//host" and "/\host" — both navigate cross-origin.
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            return false;

        return true;
    }

    /// <summary>
    /// Returns <paramref name="url" /> when it is a safe local path, otherwise
    /// <paramref name="fallback" />.
    /// </summary>
    public static string OrDefault(string? url, string fallback) => IsLocal(url) ? url! : fallback;
}
