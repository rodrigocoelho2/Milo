using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Push.Models.DTOs;
using BodyOps.WebApi.Features.Push.Models.Entities;
using BodyOps.WebApi.Features.Push.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BodyOps.UnitTest.Push;

/// <summary>
/// Which reminders go out when, what they say, and that each goes out once.
/// </summary>
public sealed class ReminderTests
{
    private static readonly DateOnly Today = TestSupport.Tuesday;
    private static readonly SettingsDTO Defaults = SettingService.ToDTO(null);

    [Fact]
    public void Meal_GoesOutAtItsTime_WithThePlateAndButtons_UntilEaten()
    {
        var day = Day(new DayMealDTO { PlanMealId = 4, Name = "Breakfast", Time = "08:00", Foods = "Oats 80 g · Whey 30 g · Banana 1 pc", Macros = new MacrosDTO { Kcal = 520.4m, ProteinG = 36.2m } });

        Assert.Empty(Plan(At(7, 59), day));
        var reminder = Assert.Single(Plan(At(8, 0), day));
        Assert.Equal("meal:2026-10-13:4", reminder.Key);
        Assert.Equal("Breakfast at 08:00", reminder.Message.Title);
        Assert.Equal("Oats 80 g, Whey 30 g, Banana 1 pc · 520 kcal · 36 g protein.", reminder.Message.Body);
        Assert.Equal("/today?meal=4", reminder.Message.Url);
        Assert.Equal(["ate", "other"], reminder.Message.Actions!.Select(static action => action.Action));
        Assert.Equal("4", reminder.Message.Data!["meal"]);
        Assert.Single(Plan(At(8, 44), day));
        Assert.Empty(Plan(At(8, 45), day));

        day.Meals[0].Eaten = true;
        Assert.Empty(Plan(At(8, 1), day));
    }

    [Fact]
    public void Meal_WithoutATime_OrRemindersOff_SendsNothing()
    {
        var day = Day(new DayMealDTO { PlanMealId = 1, Name = "Snack" });
        Assert.Empty(Plan(At(8, 0), day));

        day.Meals[0].Time = "08:00";
        var off = SettingService.ToDTO(null);
        off.Reminders!.Meals = false;
        off.Reminders.WeighIn = false;
        Assert.Empty(ReminderPlanner.Plan(At(8, 0), day, off, [], null));
    }

    [Fact]
    public void Workout_OnlyOnATrainingDay_NothingLogged()
    {
        var day = Day();
        day.Name = "Upper body";
        day.SetsPlanned = 12;
        day.Exercises.Add(new DayExerciseDTO { Name = "Bench press" });
        day.Exercises.Add(new DayExerciseDTO { Name = "Row" });

        var reminder = Assert.Single(Plan(At(18, 0), day));
        Assert.Equal(("workout:2026-10-13", "Time to train", "Upper body · 2 exercises · 12 sets."), (reminder.Key, reminder.Message.Title, reminder.Message.Body));
        Assert.Empty(Plan(At(20, 0), day));

        day.SetsDone = 1;
        Assert.Empty(Plan(At(18, 5), day));

        day.SetsDone = 0;
        day.TrainingState = TrainingState.Missed;
        Assert.Empty(Plan(At(18, 5), day));

        Assert.Empty(Plan(At(18, 5), Day()));   // rest day
    }

    [Fact]
    public void WeighIn_OnlyOnTheDueDay_AndNotOnceDone()
    {
        var settings = SettingService.ToDTO(null);
        Assert.Single(ReminderPlanner.Plan(At(8, 0), Day(), settings, [], null));   // every day, never weighed: due
        settings.WeighEveryDays = 15;

        var notDue = new List<WeightEntryDTO> { new() { Date = Today.AddDays(-11), WeightKg = 82.4m } };
        Assert.Empty(ReminderPlanner.Plan(At(8, 0), Day(), settings, notDue, null));

        var due = new List<WeightEntryDTO> { new() { Date = Today.AddDays(-15), WeightKg = 82.4m } };
        var reminder = Assert.Single(ReminderPlanner.Plan(At(8, 0), Day(), settings, due, null));
        Assert.Equal(("weigh:2026-10-13", "Weigh-in day", "15 days since 82.4 kg. Step on the scale before breakfast."), (reminder.Key, reminder.Message.Title, reminder.Message.Body));
        Assert.Equal("weigh", Assert.Single(reminder.Message.Actions!).Action);
        Assert.Empty(ReminderPlanner.Plan(At(11, 0), Day(), settings, due, null));   // window over

        due.Add(new WeightEntryDTO { Date = Today, WeightKg = 82.1m });
        Assert.Empty(ReminderPlanner.Plan(At(8, 30), Day(), settings, due, null));
    }

    [Fact]
    public void Streak_AtRisk_SaysWhatIsLeft()
    {
        var day = Day(new DayMealDTO { PlanMealId = 1, Name = "Lunch", Eaten = true }, new DayMealDTO { PlanMealId = 2, Name = "Dinner" });
        day.Score = 40;
        var streak = new StreakDTO { Threshold = 60, ThroughYesterday = 15 };

        var reminder = Assert.Single(ReminderPlanner.Plan(At(21, 0), day, Defaults, [], streak));
        Assert.Equal(("streak:2026-10-13", "Streak at risk", "Only dinner is left. Tick it to keep your 15-day streak."), (reminder.Key, reminder.Message.Title, reminder.Message.Body));

        day.SetsPlanned = 9;
        day.Exercises.Add(new DayExerciseDTO { Name = "Squat" });
        day.SetsDone = 3;
        day.Meals[0].Eaten = false;
        var two = Assert.Single(ReminderPlanner.Plan(At(21, 0), day, Defaults, [], streak));
        Assert.Equal("Lunch, dinner and the workout are left. Today needs 60 to keep your 15-day streak.", two.Message.Body);

        day.Score = 60;
        Assert.Empty(ReminderPlanner.Plan(At(21, 0), day, Defaults, [], streak));
        day.Score = 40;
        Assert.Empty(ReminderPlanner.Plan(At(21, 0), day, Defaults, [], new StreakDTO { Threshold = 60, ThroughYesterday = 0 }));   // nothing at stake
        Assert.Empty(ReminderPlanner.Plan(At(21, 0), Day(), Defaults, [], streak));   // a rest day doesn't count
    }

    [Fact]
    public async Task Worker_SendsEachReminderOnce_ToTheAccountsDevices()
    {
        var plan = new FakePlanRepository();
        plan.Meals.Add(new PlanMeal { PlanMealID = 7, Weekday = 2, Name = "Lunch", MealTime = new TimeSpan(12, 30, 0) });
        var weights = new FakeWeightRepository();
        var settings = new FakeSettingRepository();
        var clock = new FakeClock(Today) { Now = Today.ToDateTime(new TimeOnly(12, 31)) };
        var push = new FakePushRepository();
        push.Devices.Add(new PushSubscriptionRow { PushSubscriptionID = 1, UserID = 1, Endpoint = "https://fcm.googleapis.com/fcm/send/a" });
        push.Devices.Add(new PushSubscriptionRow { PushSubscriptionID = 2, UserID = 1, Endpoint = "https://web.push.apple.com/b" });
        var sender = new RecordingSender();
        var worker = new ReminderWorker(
            push,
            sender,
            new DayService(new FakeDayRepository(plan), plan, weights, clock, TestSupport.Globalization),
            new SettingService(settings, TestSupport.Globalization),
            new WeightService(weights, settings, clock, TestSupport.Globalization),
            clock,
            TestSupport.Options(),
            NullLogger<ReminderWorker>.Instance);

        Assert.Equal(1, await worker.RunOnceAsync());
        Assert.Equal(0, await worker.RunOnceAsync());   // claimed: not again

        var (devices, message) = Assert.Single(sender.Sent);
        Assert.Equal(2, devices);
        Assert.Equal("Lunch at 12:30", message.Title);
        Assert.Contains((1L, "meal:2026-10-13:7"), push.Sent);
    }

    /// <summary>Plans with the weigh-in reminder off (every day at 08:00 by default, and nobody weighed in here).</summary>
    private static List<Reminder> Plan(DateTime now, DayDTO day)
    {
        var settings = SettingService.ToDTO(null);
        settings.Reminders!.WeighIn = false;
        return ReminderPlanner.Plan(now, day, settings, [], null);
    }

    private static DateTime At(int hour, int minute) => Today.ToDateTime(new TimeOnly(hour, minute));

    private static DayDTO Day(params DayMealDTO[] meals) => new() { Date = Today, Weekday = 2, Meals = [.. meals] };

    private sealed class RecordingSender : IPushSender
    {
        public List<(int Devices, PushMessage Message)> Sent { get; } = [];

        public Task<string> PublicKeyAsync() => Task.FromResult("key");

        public Task<(int Reached, int Failed)> SendAsync(IReadOnlyList<PushSubscriptionRow> devices, IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken = default)
        {
            Sent.AddRange(messages.Select(message => (devices.Count, message)));
            return Task.FromResult((devices.Count, 0));
        }
    }
}
