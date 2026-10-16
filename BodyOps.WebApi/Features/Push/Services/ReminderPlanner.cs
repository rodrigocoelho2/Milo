using System.Globalization;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Push.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.WebApi.Features.Push.Services;

/// <summary>
/// One reminder due now: the key it is sent under once, and the notification.
/// </summary>
/// <param name="Key">Unique per account and day ("meal:2026-10-16:12").</param>
/// <param name="Message">The notification.</param>
internal sealed record Reminder(string Key, PushMessage Message);

/// <summary>
/// Which reminders are due at a given minute. Pure: the worker passes the day, the settings and the weigh-ins,
/// and sends what comes back (each key only once).
/// </summary>
internal static class ReminderPlanner
{
    /// <summary>How long after a meal's time its reminder may still go out (a server restart, a slow minute).</summary>
    public static readonly TimeSpan MealWindow = TimeSpan.FromMinutes(45);

    /// <summary>How long after the workout time its reminder may still go out.</summary>
    public static readonly TimeSpan WorkoutWindow = TimeSpan.FromHours(2);

    /// <summary>How long after the weigh-in time its reminder may still go out.</summary>
    public static readonly TimeSpan WeighInWindow = TimeSpan.FromHours(3);

    /// <summary>How long after the streak time its reminder may still go out.</summary>
    public static readonly TimeSpan StreakWindow = TimeSpan.FromHours(2);

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>
    /// The reminders due at <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The wall clock in the app's time zone.</param>
    /// <param name="day">Today.</param>
    /// <param name="settings">The account's settings.</param>
    /// <param name="weights">The weigh-ins, any order.</param>
    /// <param name="streak">The streak, or null.</param>
    /// <returns>The reminders, in the order to send them.</returns>
    public static List<Reminder> Plan(DateTime now, DayDTO day, SettingsDTO settings, IReadOnlyList<WeightEntryDTO> weights, StreakDTO? streak)
    {
        var reminders = new List<Reminder>();
        var today = DateOnly.FromDateTime(now);
        if (day.Date != today)
        {
            return reminders;
        }

        var date = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var time = now.TimeOfDay;
        var options = settings.Reminders ?? new ReminderSettingsDTO();

        if (options.WeighIn != false && Within(time, options.WeighInTime, WeighInWindow) && Weigh(today, settings, weights) is { } weigh)
        {
            reminders.Add(new Reminder($"weigh:{date}", weigh));
        }

        if (options.Meals != false)
        {
            foreach (var meal in day.Meals)
            {
                if (!meal.Eaten && Within(time, meal.Time, MealWindow))
                {
                    reminders.Add(new Reminder($"meal:{date}:{meal.PlanMealId}", Meal(date, meal)));
                }
            }
        }

        if (options.Workout != false && Within(time, options.WorkoutTime, WorkoutWindow) && Untouched(day))
        {
            reminders.Add(new Reminder($"workout:{date}", Workout(day)));
        }

        if (options.Streak != false && Within(time, options.StreakTime, StreakWindow) && StreakAtRisk(day, streak) is { } risk)
        {
            reminders.Add(new Reminder($"streak:{date}", risk));
        }

        return reminders;
    }

    /// <summary>
    /// The meal reminder: "Breakfast at 08:00", what's on the plate and its calories and protein.
    /// </summary>
    /// <param name="date">The day (yyyy-MM-dd).</param>
    /// <param name="meal">The meal.</param>
    /// <returns>The notification.</returns>
    internal static PushMessage Meal(string date, DayMealDTO meal)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(meal.Foods))
        {
            parts.Add(meal.Foods.Replace(" · ", ", ", StringComparison.Ordinal).Trim());
        }

        if (meal.Macros is { Kcal: { } kcal })
        {
            parts.Add($"{Math.Round(kcal).ToString("#,0", English)} kcal");
        }

        if (meal.Macros is { ProteinG: { } protein })
        {
            parts.Add($"{Math.Round(protein).ToString("0", English)} g protein");
        }

        var name = string.IsNullOrWhiteSpace(meal.Name) ? "Meal" : meal.Name.Trim();
        return new PushMessage(
            meal.Time is { Length: > 0 } at ? $"{name} at {at}" : name,
            parts.Count > 0 ? string.Join(" · ", parts) + "." : "Tick it when you've eaten.",
            $"/today?meal={meal.PlanMealId}",
            $"meal-{date}-{meal.PlanMealId}",
            [new PushAction("ate", "Ate it"), new PushAction("other", "Something else")],
            new Dictionary<string, string> { ["date"] = date, ["meal"] = meal.PlanMealId.ToString(CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// The weigh-in reminder, when one is due today and today has no weigh-in yet.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <param name="settings">The settings (how often).</param>
    /// <param name="weights">The weigh-ins.</param>
    /// <returns>The notification, or null when not due.</returns>
    internal static PushMessage? Weigh(DateOnly today, SettingsDTO settings, IReadOnlyList<WeightEntryDTO> weights)
    {
        var last = weights.Where(entry => entry.Date <= today).OrderByDescending(static entry => entry.Date).FirstOrDefault();
        if (!WeighInSchedule.IsDue(last?.Date, settings.WeighEveryDays ?? 1, today))
        {
            return null;
        }

        var body = "Step on the scale before breakfast.";
        if (last is not null)
        {
            var days = today.DayNumber - last.Date.DayNumber;
            body = $"{days} {(days == 1 ? "day" : "days")} since {last.WeightKg.ToString("0.0", English)} kg. {body}";
        }

        var date = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new PushMessage("Weigh-in day", body, "/today?weigh=1", $"weigh-{date}", [new PushAction("weigh", "Log weight")], new Dictionary<string, string> { ["date"] = date });
    }

    /// <summary>
    /// Whether today is a training day nothing was logged on yet.
    /// </summary>
    /// <param name="day">Today.</param>
    /// <returns>True when the workout reminder applies.</returns>
    internal static bool Untouched(DayDTO day) =>
        day.Exercises.Count > 0
        && day.TrainingState == TrainingState.Pending
        && day.SetsDone == 0
        && day.Exercises.All(static exercise => exercise.CardioMinutes is null or 0 && exercise.CardioKm is null or 0);

    /// <summary>
    /// The streak reminder: today counts, is still under the threshold, something can still be ticked and a run is at stake.
    /// </summary>
    /// <param name="day">Today.</param>
    /// <param name="streak">The streak.</param>
    /// <returns>The notification, or null.</returns>
    internal static PushMessage? StreakAtRisk(DayDTO day, StreakDTO? streak)
    {
        if (streak is null || streak.ThroughYesterday <= 0 || !WeekBuilder.Counts(day) || day.Score >= streak.Threshold)
        {
            return null;
        }

        var left = day.Meals.Where(static meal => !meal.Eaten).Select(static meal => meal.Name.Trim()).Where(static name => name.Length > 0).ToList();
        if (day.Exercises.Count > 0 && day.TrainingState == TrainingState.Pending)
        {
            left.Add("the workout");
        }

        if (left.Count == 0)
        {
            return null;
        }

        var run = streak.ThroughYesterday;
        var body = left.Count == 1
            ? $"Only {Lower(left[0])} is left. Tick it to keep your {run}-day streak."
            : $"{Capital(Join(left.Select(Lower).ToList()))} are left. Today needs {streak.Threshold} to keep your {run}-day streak.";
        var date = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new PushMessage("Streak at risk", body, "/today", $"streak-{date}");
    }

    private static PushMessage Workout(DayDTO day)
    {
        var name = string.IsNullOrWhiteSpace(day.Name) ? "Today's session" : day.Name.Trim();
        var count = day.Exercises.Count;
        var body = $"{name} · {count} {(count == 1 ? "exercise" : "exercises")}" + (day.SetsPlanned > 0 ? $" · {day.SetsPlanned} sets." : ".");
        return new PushMessage("Time to train", body, "/today", $"workout-{day.Date:yyyy-MM-dd}");
    }

    /// <summary>Whether <paramref name="time"/> is at or after <paramref name="start"/> and within the window.</summary>
    internal static bool Within(TimeSpan time, string? start, TimeSpan window) =>
        MealTimeText.TryParse(start, out var at) && at is { } from && time >= from && time < from + window;

    private static string Join(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    private static string Lower(string text) => text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    private static string Capital(string text) => text.Length > 0 ? char.ToUpperInvariant(text[0]) + text[1..] : text;
}
