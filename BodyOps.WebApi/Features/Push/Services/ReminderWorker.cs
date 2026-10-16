using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Push.Repositories;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Services;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Push.Services;

/// <summary>
/// Once a minute: for every account with a device that turned push on, works out the reminders due now
/// (<see cref="ReminderPlanner"/>), claims each so it goes out once, and sends it.
/// </summary>
/// <param name="pushRepository">Devices and the reminders already sent.</param>
/// <param name="pushSender">Sends notifications.</param>
/// <param name="dayService">Today.</param>
/// <param name="settingService">Reminder settings.</param>
/// <param name="weightService">Weigh-ins.</param>
/// <param name="clock">The app clock.</param>
/// <param name="options">The app options.</param>
/// <param name="logger">The logger.</param>
internal sealed class ReminderWorker(
    IPushRepository pushRepository,
    IPushSender pushSender,
    IDayService dayService,
    ISettingService settingService,
    IWeightService weightService,
    IAppClock clock,
    IOptions<BodyOpsOptions> options,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    /// <summary>
    /// Sends the reminders due now to every account with push on.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many reminders went out.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var devices = await pushRepository.ListAsync(null);
        var sent = 0;
        foreach (var account in devices.GroupBy(static device => device.UserID))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                sent += await RemindAsync(account.Key, [.. account], cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Reminders for an account failed");
            }
        }

        return sent;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.PushEnabled || !options.Value.RemindersEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wake a few seconds after each minute starts, so "18:00" goes out at 18:00.
            var now = DateTime.UtcNow;
            var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(1).AddSeconds(5);
            try
            {
                await Task.Delay(next - now, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The reminder worker failed; it tries again next minute");
            }
        }
    }

    private async Task<int> RemindAsync(long userId, List<Models.Entities.PushSubscriptionRow> devices, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var settings = Value(await settingService.GetAsync(userId));
        var week = Value(await dayService.GetWeekAsync(userId, clock.Today));
        var day = week?.Days.FirstOrDefault(day => day.Date == clock.Today);
        if (settings is null || day is null)
        {
            return 0;
        }

        var weights = Value(await weightService.ListAsync(userId))?.Entries ?? [];
        StreakDTO? streak = null;
        if (settings.Reminders?.Streak != false && ReminderPlanner.Within(now.TimeOfDay, settings.Reminders?.StreakTime, ReminderPlanner.StreakWindow))
        {
            streak = Value(await dayService.GetStreakAsync(userId));
        }

        var sent = 0;
        foreach (var reminder in ReminderPlanner.Plan(now, day, settings, weights, streak))
        {
            if (!await pushRepository.ClaimAsync(userId, reminder.Key))
            {
                continue;
            }

            await pushSender.SendAsync(devices, [reminder.Message], cancellationToken);
            sent++;
        }

        return sent;
    }

    private static T? Value<T>(ServiceApiResponse<T> response) where T : class => response.IsSuccess ? response.Result : null;
}
