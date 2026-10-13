using BodyOps.Library.Enums;
using BodyOps.Library.Services;

namespace BodyOps.WebApi.Extensions.Globalization;

/// <summary>
/// Shortcuts that build translated failure responses.
/// </summary>
internal static class GlobalizationHelperExtensions
{
    /// <summary>
    /// A validation failure (400) about one field.
    /// </summary>
    /// <typeparam name="T">The payload type of the response.</typeparam>
    /// <param name="globalizationHelper">Translations.</param>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The response.</returns>
    public static async Task<ServiceApiResponse<T>> ValidationProblemAsync<T>(this IGlobalizationHelper globalizationHelper, string field, string messageKey, params object[] arguments)
    {
        var error = await globalizationHelper.ValidationErrorAsync(field, messageKey, arguments);
        return ServiceApiResponse<T>.ValidationProblems(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
            .AddError(error.Field, error.Message);
    }

    /// <summary>
    /// A missing record (404).
    /// </summary>
    /// <typeparam name="T">The payload type of the response.</typeparam>
    /// <param name="globalizationHelper">Translations.</param>
    /// <param name="identifier">The identifier that was asked for.</param>
    /// <returns>The response.</returns>
    public static async Task<ServiceApiResponse<T>> NotFoundAsync<T>(this IGlobalizationHelper globalizationHelper, object identifier) =>
        ServiceApiResponse<T>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier));
}
