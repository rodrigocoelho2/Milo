namespace BodyOps.WebApi.Features.Health.Models.DTOs;

/// <summary>
/// What the page needs before signing in: is the server up, is it the first run, is sign-up open.
/// </summary>
public sealed class HealthDTO
{
    /// <summary>Gets or sets the status ("ok").</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Gets or sets a value indicating whether there are no accounts yet (the next one becomes the admin).</summary>
    public bool FirstRun { get; set; }

    /// <summary>Gets or sets a value indicating whether a new account can be created.</summary>
    public bool Registration { get; set; }

    /// <summary>Gets or sets a value indicating whether "Sign in with Discord" is set up.</summary>
    public bool Discord { get; set; }
}
