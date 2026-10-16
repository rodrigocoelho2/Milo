using BodyOps.Library.Validation;

namespace BodyOps.Library.Services;

/// <summary>
/// The outcome kind of a service call; the WebApi maps it to an HTTP status code.
/// </summary>
public enum ServiceApiResponseStatus
{
    /// <summary>200 OK with a payload, or 204 No Content without one.</summary>
    Success,

    /// <summary>201 Created.</summary>
    Created,

    /// <summary>202 Accepted: the work goes on in the background.</summary>
    Accepted,

    /// <summary>400 Bad Request with validation problems.</summary>
    ValidationProblems,

    /// <summary>401 Unauthorized (wrong credentials).</summary>
    Unauthorized,

    /// <summary>403 Forbidden (not allowed on this server).</summary>
    Forbidden,

    /// <summary>404 Not Found.</summary>
    NotFound,

    /// <summary>409 Conflict, with problems or with the current record as payload.</summary>
    Conflict,

    /// <summary>413 Payload Too Large.</summary>
    PayloadTooLarge,

    /// <summary>503 Service Unavailable: an outside service did not answer, or asked to slow down.</summary>
    Unavailable,
}

/// <summary>
/// The result of every service method: either a value, or problems. Services never throw for
/// expected failures and never pick an HTTP status code themselves.
/// </summary>
/// <typeparam name="T">The payload type.</typeparam>
public sealed class ServiceApiResponse<T>
{
    private ServiceApiResponse(ServiceApiResponseStatus status, T? result, ApiValidationProblemDetails? problems)
    {
        Status = status;
        Result = result;
        Problems = problems;
    }

    /// <summary>Gets the outcome kind.</summary>
    public ServiceApiResponseStatus Status { get; }

    /// <summary>Gets the payload (on success, accepted, or a conflict that returns the current record).</summary>
    public T? Result { get; }

    /// <summary>Gets the problems (only on failure).</summary>
    public ApiValidationProblemDetails? Problems { get; }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Status is ServiceApiResponseStatus.Success or ServiceApiResponseStatus.Created or ServiceApiResponseStatus.Accepted;

    /// <summary>
    /// A successful response carrying a payload.
    /// </summary>
    /// <param name="result">The payload.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Success(T result) => new(ServiceApiResponseStatus.Success, result, null);

    /// <summary>
    /// A successful response with no payload.
    /// </summary>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Success() => new(ServiceApiResponseStatus.Success, default, null);

    /// <summary>
    /// A record was created (HTTP 201).
    /// </summary>
    /// <param name="result">The new record.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Created(T result) => new(ServiceApiResponseStatus.Created, result, null);

    /// <summary>
    /// Work accepted and running in the background (HTTP 202).
    /// </summary>
    /// <param name="result">The current progress.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Accepted(T result) => new(ServiceApiResponseStatus.Accepted, result, null);

    /// <summary>
    /// A validation failure (HTTP 400).
    /// </summary>
    /// <param name="title">The translated summary.</param>
    /// <returns>The response; add details with <see cref="AddError"/> / <see cref="AddErrorRange"/>.</returns>
    public static ServiceApiResponse<T> ValidationProblems(string title) => Failure(ServiceApiResponseStatus.ValidationProblems, title, 400);

    /// <summary>
    /// Wrong credentials (HTTP 401).
    /// </summary>
    /// <param name="title">The translated message.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Unauthorized(string title) => Failure(ServiceApiResponseStatus.Unauthorized, title, 401);

    /// <summary>
    /// Not allowed on this server (HTTP 403).
    /// </summary>
    /// <param name="title">The translated message.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Forbidden(string title) => Failure(ServiceApiResponseStatus.Forbidden, title, 403);

    /// <summary>
    /// A missing record (HTTP 404).
    /// </summary>
    /// <param name="title">The translated summary.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> NotFound(string title) => Failure(ServiceApiResponseStatus.NotFound, title, 404);

    /// <summary>
    /// A conflict explained by a message (HTTP 409).
    /// </summary>
    /// <param name="title">The translated message.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Conflict(string title) => Failure(ServiceApiResponseStatus.Conflict, title, 409);

    /// <summary>
    /// A conflict that returns the current record so the caller can merge (HTTP 409).
    /// </summary>
    /// <param name="current">The current record.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Conflict(T current) => new(ServiceApiResponseStatus.Conflict, current, null);

    /// <summary>
    /// The request body is too large (HTTP 413).
    /// </summary>
    /// <param name="title">The translated message.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> PayloadTooLarge(string title) => Failure(ServiceApiResponseStatus.PayloadTooLarge, title, 413);

    /// <summary>
    /// 503: an outside service is not available right now.
    /// </summary>
    /// <param name="title">The translated message.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Unavailable(string title) => Failure(ServiceApiResponseStatus.Unavailable, title, 503);

    /// <summary>
    /// Adds one error to a failure response.
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="message">The translated message.</param>
    /// <returns>This response, for chaining.</returns>
    public ServiceApiResponse<T> AddError(string field, string message)
    {
        if (Problems is not null)
        {
            Problems.Errors.Add(new ApiValidationError(field, message));
            Problems.Detail = Problems.Errors[0].Message;
        }

        return this;
    }

    /// <summary>
    /// Adds several errors to a failure response.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    /// <returns>This response, for chaining.</returns>
    public ServiceApiResponse<T> AddErrorRange(IEnumerable<ApiValidationError> errors)
    {
        foreach (var error in errors)
        {
            AddError(error.Field, error.Message);
        }

        return this;
    }

    private static ServiceApiResponse<T> Failure(ServiceApiResponseStatus status, string title, int code) =>
        new(status, default, new ApiValidationProblemDetails { Title = title, Detail = title, Status = code });
}
