using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.UnitTest.Settings;

public sealed class SettingAndScheduleTests
{
    private static readonly DateOnly Today = TestSupport.Tuesday;

    [Fact]
    public void NextDue_EveryFifteenDays_CountsFromTheLastWeighIn()
    {
        Assert.Equal(Today.AddDays(4), WeighInSchedule.NextDue(Today.AddDays(-11), 15, Today));
        Assert.Equal(Today, WeighInSchedule.NextDue(Today.AddDays(-15), 15, Today));
        Assert.Equal(Today, WeighInSchedule.NextDue(Today.AddDays(-30), 15, Today));   // overdue: today
        Assert.Equal(Today.AddDays(15), WeighInSchedule.NextDue(Today, 15, Today));    // weighed today
        Assert.Equal(Today, WeighInSchedule.NextDue(null, 15, Today));                 // never weighed
    }

    [Fact]
    public void NextDue_EveryDay_IsTodayUntilDone()
    {
        Assert.Equal(Today, WeighInSchedule.NextDue(Today.AddDays(-1), 1, Today));
        Assert.Equal(Today.AddDays(1), WeighInSchedule.NextDue(Today, 1, Today));
        Assert.True(WeighInSchedule.IsDue(Today.AddDays(-3), 1, Today));
        Assert.False(WeighInSchedule.IsDue(Today, 1, Today));
        Assert.False(WeighInSchedule.IsDue(Today.AddDays(-3), 15, Today));
        Assert.True(WeighInSchedule.IsDue(Today.AddDays(-15), 15, Today));
    }

    [Fact]
    public async Task Settings_HaveDefaults_SavesKeepWhatIsLeftOut_AndCheckValues()
    {
        var repository = new FakeSettingRepository();
        var service = new SettingService(repository, TestSupport.Globalization);

        var defaults = (await service.GetAsync(1)).Result!;
        Assert.Equal((90, 1), (defaults.RestSeconds, defaults.WeighEveryDays));
        Assert.Equal(("18:00", "08:00", "21:00"), (defaults.Reminders!.WorkoutTime, defaults.Reminders.WeighInTime, defaults.Reminders.StreakTime));
        Assert.True(defaults.Reminders.Meals);

        await service.SaveAsync(1, new SettingsDTO { WeighEveryDays = 15, Reminders = new ReminderSettingsDTO { Meals = false, WorkoutTime = "17:30" } });
        var saved = (await service.SaveAsync(1, new SettingsDTO { WeightGoalKg = 80, RestSeconds = 120 })).Result!;
        Assert.Equal((80m, 120, 15), (saved.WeightGoalKg!.Value, saved.RestSeconds!.Value, saved.WeighEveryDays!.Value));
        Assert.False(saved.Reminders!.Meals);
        Assert.Equal("17:30", saved.Reminders.WorkoutTime);
        Assert.True(saved.Reminders.Streak);

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.SaveAsync(1, new SettingsDTO { WeighEveryDays = 61 })).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.SaveAsync(1, new SettingsDTO { Reminders = new ReminderSettingsDTO { StreakTime = "late" } })).Status);
    }
}
