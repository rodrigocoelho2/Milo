using System.Net.Mime;
using BodyOps.Library.Services;
using Microsoft.AspNetCore.Mvc;

namespace BodyOps.WebApi.Extensions.Services;

/// <summary>
/// Maps a <see cref="ServiceApiResponse{T}"/> to the HTTP result. Controllers never pick status codes themselves.
/// </summary>
internal static class ServiceApiResponseExtensions
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>
    /// Converts a service response into an action result.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="response">The service response.</param>
    /// <param name="location">The URL of the new record, for <see cref="ServiceApiResponseStatus.Created"/>.</param>
    /// <returns>
    /// 200 with the payload (204 without one), 201, 202, 409 with the current record,
    /// or the problem details with the status code of the failure.
    /// </returns>
    public static ActionResult ToActionResult<T>(this ServiceApiResponse<T> response, string? location = null) => response.Status switch
    {
        ServiceApiResponseStatus.Success when response.Result is null => new NoContentResult(),
        ServiceApiResponseStatus.Success => new OkObjectResult(response.Result),
        ServiceApiResponseStatus.Created => new CreatedResult(location ?? string.Empty, response.Result),
        ServiceApiResponseStatus.Accepted => new AcceptedResult(location ?? string.Empty, response.Result),
        ServiceApiResponseStatus.Conflict when response.Problems is null => new ConflictObjectResult(response.Result),
        _ when response.Problems is null => new StatusCodeResult(StatusCodes.Status500InternalServerError),
        _ => Problem(response),
    };

    private static ObjectResult Problem<T>(ServiceApiResponse<T> response)
    {
        var result = new ObjectResult(response.Problems) { StatusCode = response.Problems!.Status };
        result.ContentTypes.Add(ProblemJson);
        result.ContentTypes.Add(MediaTypeNames.Application.Json);
        return result;
    }
}
