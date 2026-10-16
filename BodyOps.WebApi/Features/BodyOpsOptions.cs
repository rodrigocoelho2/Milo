namespace BodyOps.WebApi.Features;

/// <summary>
/// Settings of the "BodyOps" configuration section (env <c>BodyOps__Name</c>).
/// Relative folders are resolved from the content root at startup.
/// </summary>
public sealed class BodyOpsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "BodyOps";

    /// <summary>Gets or sets the folder for the login keys (so sessions survive restarts and updates).</summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>Gets or sets a value indicating whether people can create their own account. The first account can always be created, and becomes the admin.</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether X-Forwarded-For / X-Forwarded-Proto from a reverse proxy are trusted.</summary>
    public bool BehindProxy { get; set; }

    /// <summary>Gets or sets the sign-in, sign-up and password attempts allowed per minute from one address.</summary>
    public int AuthAttemptsPerMinute { get; set; } = 10;

    /// <summary>Gets or sets the IANA time zone that decides what "today" and "this week" are.</summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>Gets or sets the largest accepted backup file, in bytes.</summary>
    public int MaxBackupBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>Gets or sets how Open Food Facts can reach whoever runs this server (it asks apps to say so in the User-Agent).</summary>
    public string OpenFoodFactsContact { get; set; } = "self-hosted; https://github.com/rodrigocoelho2/Milo";

    /// <summary>
    /// Makes the folders absolute.
    /// </summary>
    /// <param name="contentRoot">The content root path.</param>
    public void ResolvePaths(string contentRoot)
    {
        DataDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(DataDirectory) ? "data" : DataDirectory, contentRoot);
    }
}
