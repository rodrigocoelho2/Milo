using BodyOps.Library.Constants;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// CSRF guard for the cookie-authenticated API: every request that changes something must carry
/// the X-BodyOps header. Another website can't add a custom header to a cross-site request
/// without a CORS preflight, which this app never allows. Registered as a global MVC filter.
/// </summary>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class AppHeaderFilter(IGlobalizationHelper globalizationHelper) : IAsyncResourceFilter
{
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || request.Headers.ContainsKey(ApiHeaders.App)
            || context.ActionDescriptor.EndpointMetadata.OfType<NoAppHeaderAttribute>().Any())
        {
            await next();
            return;
        }

        var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.MissingAppHeader), ApiHeaders.App);
        context.Result = ServiceApiResponse<string>.ValidationProblems(message).ToActionResult();
    }
}

/// <summary>
/// For an endpoint that doesn't use the sign-in cookie (it authenticates with a key in the request), so a cross-site
/// request can't act as anyone and the X-BodyOps header isn't needed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class NoAppHeaderAttribute : Attribute
{
}
