namespace BodyOps.Library.Validation;

/// <summary>
/// One problem with one field of a request.
/// </summary>
/// <param name="Field">The field identifier (empty for the request as a whole).</param>
/// <param name="Message">The translated message.</param>
public sealed record ApiValidationError(string Field, string Message);
