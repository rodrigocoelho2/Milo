using BodyOps.WebApi.Features;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Extensions.Time;

/// <summary>
/// "Today" and "this week" in the configured time zone. Weeks start on Monday; weekdays are 1 (Monday) … 7 (Sunday).
/// </summary>
internal interface IAppClock
{
    /// <summary>Gets today's date in the configured time zone.</summary>
    DateOnly Today { get; }

    /// <summary>Gets the wall-clock time in the configured time zone.</summary>
    DateTime Now { get; }

    /// <summary>Gets the configured time zone.</summary>
    TimeZoneInfo Zone { get; }
}

/// <summary>
/// The clock from <see cref="TimeProvider"/> and <see cref="BodyOpsOptions.TimeZone"/>.
/// </summary>
/// <param name="timeProvider">The time source.</param>
/// <param name="options">The BodyOps options.</param>
internal sealed class AppClock(TimeProvider timeProvider, IOptions<BodyOpsOptions> options) : IAppClock
{
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly TimeZoneInfo _zone = FindZone(options.Value.TimeZone);

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(Now);

    /// <inheritdoc />
    public DateTime Now => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _zone).DateTime;

    /// <inheritdoc />
    public TimeZoneInfo Zone => _zone;

    private static TimeZoneInfo FindZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "UTC" : id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

/// <summary>
/// Week arithmetic shared by the services.
/// </summary>
internal static class WeekMath
{
    /// <summary>
    /// The weekday number used everywhere in BodyOps.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>1 for Monday … 7 for Sunday.</returns>
    public static int Weekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    /// <summary>
    /// The Monday of the week a date falls in.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The Monday on or before the date.</returns>
    public static DateOnly Monday(DateOnly date) => date.AddDays(1 - Weekday(date));
}
