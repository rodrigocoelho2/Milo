using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Services;
using Microsoft.AspNetCore.Mvc;

namespace BodyOps.UnitTest.Extensions;

public sealed class ServiceApiResponseExtensionsTests
{
    [Fact]
    public void Success_WithAndWithoutPayload()
    {
        Assert.IsType<OkObjectResult>(ServiceApiResponse<string>.Success("x").ToActionResult());
        Assert.IsType<NoContentResult>(ServiceApiResponse<string>.Success().ToActionResult());
    }

    [Fact]
    public void CreatedAndAccepted_CarryTheLocation()
    {
        Assert.Equal("/api/auth/me", Assert.IsType<CreatedResult>(ServiceApiResponse<string>.Created("x").ToActionResult("/api/auth/me")).Location);
        Assert.Equal(202, Assert.IsType<AcceptedResult>(ServiceApiResponse<string>.Accepted("x").ToActionResult("/status")).StatusCode);
    }

    [Fact]
    public void ConflictWithTheCurrentRecord_Is409WithThePayload()
    {
        var result = Assert.IsType<ConflictObjectResult>(ServiceApiResponse<string>.Conflict(current: "current").ToActionResult());
        Assert.Equal("current", result.Value);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(413)]
    public void Failures_AreProblemDetailsWithTheirStatus(int status)
    {
        var response = status switch
        {
            400 => ServiceApiResponse<string>.ValidationProblems("Title").AddError("field", "Message"),
            401 => ServiceApiResponse<string>.Unauthorized("Title"),
            403 => ServiceApiResponse<string>.Forbidden("Title"),
            404 => ServiceApiResponse<string>.NotFound("Title"),
            409 => ServiceApiResponse<string>.Conflict("Title"),
            _ => ServiceApiResponse<string>.PayloadTooLarge("Title"),
        };

        var result = Assert.IsType<ObjectResult>(response.ToActionResult());

        Assert.Equal(status, result.StatusCode);
        Assert.Contains("application/problem+json", result.ContentTypes);
        Assert.Equal(status == 400 ? "Message" : "Title", response.Problems!.Detail);
    }
}
