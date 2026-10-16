namespace BodyOps.WebApi.Features.Weights.Services;

/// <summary>
/// When the next weigh-in is due, for accounts that weigh in every few days instead of daily. Pure, unit-tested; the
/// page has the same rule (BO.weighIn).
/// </summary>
internal static class WeighInSchedule
{
    /// <summary>The longest interval.</summary>
    public const int MaxDays = 60;

    /// <summary>
    /// The day the next weigh-in is due.
    /// </summary>
    /// <param name="lastWeighIn">The last weigh-in on or before <paramref name="today"/>, or null.</param>
    /// <param name="everyDays">The interval (1 = every day).</param>
    /// <param name="today">Today.</param>
    /// <returns>The due day: today when it is due or overdue (or there is no weigh-in yet), otherwise a later day.</returns>
    public static DateOnly NextDue(DateOnly? lastWeighIn, int everyDays, DateOnly today)
    {
        if (lastWeighIn is not { } last || everyDays <= 1)
        {
            return lastWeighIn == today && everyDays <= 1 ? today.AddDays(1) : today;
        }

        var due = last.AddDays(everyDays);
        return due <= today ? today : due;
    }

    /// <summary>
    /// Whether a weigh-in is due on a day (and hasn't been done that day).
    /// </summary>
    /// <param name="lastWeighIn">The last weigh-in on or before <paramref name="day"/>, or null.</param>
    /// <param name="everyDays">The interval.</param>
    /// <param name="day">The day.</param>
    /// <returns>True when the scale is waiting.</returns>
    public static bool IsDue(DateOnly? lastWeighIn, int everyDays, DateOnly day) =>
        lastWeighIn != day && NextDue(lastWeighIn, everyDays, day) == day;
}
