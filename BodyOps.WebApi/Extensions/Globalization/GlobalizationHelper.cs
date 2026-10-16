using System.Globalization;
using BodyOps.Library.Enums;
using BodyOps.Library.Validation;

namespace BodyOps.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error for one field.
    /// </summary>
    /// <param name="field">The field identifier (camelCase, as the page sends it).</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// English translations. The page is English-only, so the texts live in code rather than in a translation table.
/// Every message is a whole sentence: the page shows it as is.
/// </summary>
internal sealed class GlobalizationHelper : IGlobalizationHelper
{
    // Numbers in messages are formatted the same way whatever culture the server runs in.
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = "Some of the values sent are not valid.",
        [nameof(TranslationKeys.NoRecordsFound)] = "'{0}' does not exist.",
        [nameof(TranslationKeys.MissingAppHeader)] = "Missing the {0} header.",

        // Accounts
        [nameof(TranslationKeys.InvalidUsername)] = "Username must be 3–24 letters, numbers, dots, dashes or underscores.",
        [nameof(TranslationKeys.PasswordTooShort)] = "Password must have at least {0} characters.",
        [nameof(TranslationKeys.PasswordTooLong)] = "Password can have at most {0} characters.",
        [nameof(TranslationKeys.RegistrationClosed)] = "New accounts are turned off on this server. Ask the admin to create one for you.",
        [nameof(TranslationKeys.UsernameTaken)] = "That username is taken.",
        [nameof(TranslationKeys.WrongCredentials)] = "Wrong username or password.",
        [nameof(TranslationKeys.WrongCurrentPassword)] = "Your current password is not correct.",
        [nameof(TranslationKeys.WrongPassword)] = "Your password is not correct.",
        [nameof(TranslationKeys.ResetOwnPassword)] = "Use Change password for your own account.",
        [nameof(TranslationKeys.DeleteOwnAccount)] = "Delete your own account from Settings › Account instead.",

        // Plan
        [nameof(TranslationKeys.InvalidWeekday)] = "Pick a day from 1 (Monday) to 7 (Sunday).",
        [nameof(TranslationKeys.InvalidName)] = "Give it a name of at most {0} characters.",
        [nameof(TranslationKeys.TextTooLong)] = "Use at most {0} characters.",
        [nameof(TranslationKeys.TooManyItems)] = "At most {0} per day.",
        [nameof(TranslationKeys.InvalidAmount)] = "Amount must be between 0 and {0}.",
        [nameof(TranslationKeys.CopyToSameDay)] = "Pick at least one other day to copy to.",
        [nameof(TranslationKeys.InvalidTime)] = "Use a time of day like 08:00.",
        [nameof(TranslationKeys.InvalidStep)] = "The weight to add must be between {0} and {1} kg.",
        [nameof(TranslationKeys.UnknownPlanItem)] = "That item is not in the plan for this day any more. Reload the page.",

        // Foods
        [nameof(TranslationKeys.InvalidFoodValue)] = "Values must be between 0 and {0}.",
        [nameof(TranslationKeys.FoodNameTaken)] = "You already have a food called {0}.",
        [nameof(TranslationKeys.PackagedUnavailable)] = "Open Food Facts didn't answer. Try again in a moment, or type the label.",
        [nameof(TranslationKeys.PackagedSlowDown)] = "That's a lot of searches in a minute. Wait a moment and try again.",
        [nameof(TranslationKeys.InvalidBarcode)] = "A barcode has 8 to 14 digits.",

        // Days
        [nameof(TranslationKeys.InvalidLoad)] = "Load must be between 0 and {0} kg.",
        [nameof(TranslationKeys.InvalidReps)] = "Reps must be between 0 and {0}.",
        [nameof(TranslationKeys.TooManySets)] = "At most {0} sets per exercise.",
        [nameof(TranslationKeys.InvalidCardio)] = "Minutes and kilometres must be between 0 and {0}.",
        [nameof(TranslationKeys.InvalidDate)] = "Use a date between {0} and {1}.",

        // Weights
        [nameof(TranslationKeys.InvalidWeight)] = "Weight must be between {0} and {1} kg.",
        [nameof(TranslationKeys.InvalidWeighEvery)] = "Weigh in every {0} to {1} days.",
        [nameof(TranslationKeys.InvalidRest)] = "Rest must be between {0} and {1} seconds.",

        // Push
        [nameof(TranslationKeys.PushEndpointNotAllowed)] = "This browser's push service isn't one Milo knows.",
        [nameof(TranslationKeys.PushKeysInvalid)] = "This browser sent a subscription without valid keys.",
        [nameof(TranslationKeys.PushDisabled)] = "Push reminders are turned off on this server.",
        [nameof(TranslationKeys.PushNoDevices)] = "Turn on reminders on this device first.",

        // Backups
        [nameof(TranslationKeys.InvalidBackup)] = "This file is not a Milo (BodyOps) or CorpoOps backup.",
        [nameof(TranslationKeys.BackupTooLarge)] = "The backup is larger than {0} MB.",
    };

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var text = Translations.TryGetValue(key, out var translation) ? translation : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(English, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments) =>
        new(field, await TranslateAsync(messageKey, arguments));
}
