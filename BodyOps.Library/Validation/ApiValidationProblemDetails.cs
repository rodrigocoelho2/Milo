namespace BodyOps.Library.Validation;

/// <summary>
/// The body of every failed WebApi call. <see cref="Title"/> and <see cref="Detail"/> carry the message the page shows.
/// </summary>
public sealed class ApiValidationProblemDetails
{
    /// <summary>Gets or sets the translated summary.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the message to show (the first error, or the title when there are none).</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets the HTTP status code.</summary>
    public int Status { get; set; }

    /// <summary>Gets the problems, one per field.</summary>
    public List<ApiValidationError> Errors { get; } = [];
}
