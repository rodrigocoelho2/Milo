namespace BodyOps.Library.Enums;

/// <summary>
/// Keys of the user-facing messages. Use with <c>nameof(TranslationKeys.X)</c>, never a magic string.
/// </summary>
public enum TranslationKeys
{
    /// <summary>Generic title of a validation failure.</summary>
    ValidationProblems,

    /// <summary>The record does not exist.</summary>
    NoRecordsFound,

    /// <summary>A write without the X-BodyOps header.</summary>
    MissingAppHeader,

    /// <summary>The username does not follow the rules.</summary>
    InvalidUsername,

    /// <summary>The password is too short.</summary>
    PasswordTooShort,

    /// <summary>The password is too long.</summary>
    PasswordTooLong,

    /// <summary>Sign-up is closed on this server.</summary>
    RegistrationClosed,

    /// <summary>Another account already uses the username.</summary>
    UsernameTaken,

    /// <summary>Wrong username or password.</summary>
    WrongCredentials,

    /// <summary>The current password does not match.</summary>
    WrongCurrentPassword,

    /// <summary>The password does not match (delete account).</summary>
    WrongPassword,

    /// <summary>The admin tried to reset their own password.</summary>
    ResetOwnPassword,

    /// <summary>The admin tried to delete their own account from Users.</summary>
    DeleteOwnAccount,

    /// <summary>The weekday is outside 1 (Monday) – 7 (Sunday).</summary>
    InvalidWeekday,

    /// <summary>A name is empty or too long.</summary>
    InvalidName,

    /// <summary>A text field is too long.</summary>
    TextTooLong,

    /// <summary>A day has too many meals or exercises, or a meal too many foods.</summary>
    TooManyItems,

    /// <summary>An amount is negative or too large.</summary>
    InvalidAmount,

    /// <summary>A load is negative or too large.</summary>
    InvalidLoad,

    /// <summary>A rep count is negative or too large.</summary>
    InvalidReps,

    /// <summary>Too many sets for one exercise.</summary>
    TooManySets,

    /// <summary>Cardio minutes or distance out of range.</summary>
    InvalidCardio,

    /// <summary>A body weight out of range.</summary>
    InvalidWeight,

    /// <summary>A date that can't be read or is out of range.</summary>
    InvalidDate,

    /// <summary>A day picked as copy target is the source day itself.</summary>
    CopyToSameDay,

    /// <summary>The plan item does not exist on that day.</summary>
    UnknownPlanItem,

    /// <summary>The backup file is not a BodyOps or CorpoOps backup.</summary>
    InvalidBackup,

    /// <summary>The backup is larger than allowed.</summary>
    BackupTooLarge,

    /// <summary>A meal time that is not a time of day.</summary>
    InvalidTime,

    /// <summary>A rest between sets out of range.</summary>
    InvalidRest,

    /// <summary>The weight added after a completed rep range is out of range.</summary>
    InvalidStep,

    /// <summary>A food value is out of range.</summary>
    InvalidFoodValue,

    /// <summary>Another food already has that name.</summary>
    FoodNameTaken,

    /// <summary>Open Food Facts could not be reached.</summary>
    PackagedUnavailable,

    /// <summary>Too many Open Food Facts lookups in a minute.</summary>
    PackagedSlowDown,

    /// <summary>A barcode is not 8–14 digits.</summary>
    InvalidBarcode,

    /// <summary>The weigh-in interval is out of range.</summary>
    InvalidWeighEvery,

    /// <summary>A push endpoint is not one of the browsers' push services.</summary>
    PushEndpointNotAllowed,

    /// <summary>A push subscription's keys are missing or malformed.</summary>
    PushKeysInvalid,

    /// <summary>Push reminders are turned off on this server.</summary>
    PushDisabled,

    /// <summary>The account has no device with push on.</summary>
    PushNoDevices,

    /// <summary>The Shortcut sent no key, or one that isn't (or no longer is) an account's.</summary>
    WatchKeyInvalid,

    /// <summary>What the Shortcut sent can't be read.</summary>
    WatchPayloadInvalid,

    /// <summary>No account is linked to that Discord account.</summary>
    DiscordNotLinked,

    /// <summary>That Discord account is linked to another account.</summary>
    DiscordTaken,

    /// <summary>Discord's answer is missing or too old.</summary>
    DiscordExpired,

    /// <summary>Discord is the only way into the account, so it can't be unlinked before a password is set.</summary>
    DiscordNeedsPassword,
}
