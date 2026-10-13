using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Weights.Models.Entities;

namespace BodyOps.UnitTest.Days;

public sealed class DayScoreTests
{
    private static readonly PlanData Plan = new(
        [
            new PlanMeal { PlanMealID = 1, Weekday = 2, Name = "Breakfast", SortOrder = 0, MealTime = new TimeSpan(8, 0, 0) },
            new PlanMeal { PlanMealID = 2, Weekday = 2, Name = "Lunch", SortOrder = 1 },
        ],
        [],
        [
            new PlanExercise { PlanExerciseID = 10, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Pec deck", Detail = "3x12", SortOrder = 0 },
            new PlanExercise { PlanExerciseID = 11, Weekday = 2, ExerciseType = ExerciseType.Cardio, Name = "Cycling", Detail = "30 min", SortOrder = 1 },
        ])
    {
        Names = [new PlanDayName { Weekday = 2, Name = "Upper body" }],
    };

    private static DateTime At(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    private static DayRange Logged(TrainingState state, bool breakfast, int sets, decimal? cardioMinutes) => new(
        [new DayRow { DayID = 5, DayDate = At(TestSupport.Tuesday), TrainingState = state }],
        [
            new DayMealRow { DayID = 5, PlanMealID = 1, Name = "Breakfast", Eaten = breakfast, SortOrder = 0, MealTime = new TimeSpan(7, 30, 0) },
            new DayMealRow { DayID = 5, PlanMealID = 2, Name = "Lunch", SortOrder = 1 },
        ],
        [
            new DayExerciseRow { DayID = 5, DayExerciseID = 50, PlanExerciseID = 10, ExerciseType = ExerciseType.Strength, Name = "Pec deck", NameKey = "pec deck", Detail = "3x12", SortOrder = 0 },
            new DayExerciseRow { DayID = 5, DayExerciseID = 51, PlanExerciseID = 11, ExerciseType = ExerciseType.Cardio, Name = "Cycling", NameKey = "cycling", Detail = "30 min", SortOrder = 1, CardioMinutes = cardioMinutes },
        ],
        Enumerable.Range(1, sets).Select(number => new DaySetRow { DayExerciseID = 50, SetNumber = number, Reps = 12 }).ToList());

    [Fact]
    public void Day_CarriesTheDayNameAndMealTimes_FromThePlanOrTheLoggedCopy()
    {
        var unlogged = WeekBuilder.Day(TestSupport.Tuesday, Plan, DayRange.Empty, []);
        Assert.Equal("Upper body", unlogged.Name);
        Assert.Equal("08:00", unlogged.Meals[0].Time);
        Assert.Null(unlogged.Meals[1].Time);

        var logged = WeekBuilder.Day(TestSupport.Tuesday, Plan, Logged(TrainingState.Pending, true, 0, null), []);
        Assert.Equal("07:30", logged.Meals[0].Time);
    }

    [Fact]
    public void Score_WeighsTrainingMealsAndWeighIn()
    {
        // Strength 3 sets + cardio 1 = 4 planned; 2 sets + the ride done = 3. Meals 1 of 2. Weighed in.
        var weights = new List<WeightEntry> { new() { EntryDate = At(TestSupport.Tuesday), WeightKg = 97.6m } };
        var day = WeekBuilder.Day(TestSupport.Tuesday, Plan, Logged(TrainingState.Pending, true, 2, 32), [], weights);

        Assert.Equal(3, day.SetsDone);
        Assert.Equal(4, day.SetsPlanned);
        Assert.Equal(97.6m, day.WeightKg);
        Assert.Equal((int)Math.Round(50 * 0.75 + 35 * 0.5 + 15), day.Score);
    }

    [Fact]
    public void Score_IsHundred_WhenEverythingIsDone_AndTrainedCountsAllSets()
    {
        var logged = Logged(TrainingState.Done, true, 0, null);
        logged.Meals[1].Eaten = true;
        var weights = new List<WeightEntry> { new() { EntryDate = At(TestSupport.Tuesday), WeightKg = 97.6m } };

        var day = WeekBuilder.Day(TestSupport.Tuesday, Plan, logged, [], weights);

        Assert.Equal(day.SetsPlanned, day.SetsDone);
        Assert.Equal(100, day.Score);
    }

    [Fact]
    public void Score_SkippedSession_ScoresTrainingAsZero()
    {
        var day = WeekBuilder.Day(TestSupport.Tuesday, Plan, Logged(TrainingState.Missed, true, 3, null), []);

        Assert.Equal((int)Math.Round(100 * (35 * 0.5) / 100), day.Score);
    }

    [Fact]
    public void Score_RestDay_LeavesTrainingOut()
    {
        var plan = new PlanData([new PlanMeal { PlanMealID = 1, Weekday = 2, Name = "Breakfast" }], [], []);
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = At(TestSupport.Tuesday) }],
            [new DayMealRow { DayID = 5, PlanMealID = 1, Name = "Breakfast", Eaten = true }],
            [],
            []);

        var day = WeekBuilder.Day(TestSupport.Tuesday, plan, logged, []);

        Assert.Equal(0, day.SetsPlanned);
        Assert.Equal((int)Math.Round(100 * 35.0 / 50), day.Score);
        Assert.True(WeekBuilder.Counts(day));
        Assert.False(WeekBuilder.Counts(WeekBuilder.Day(TestSupport.Monday, plan, DayRange.Empty, [])));
    }

    private static DayDTO Scored(int score, bool planned = true) =>
        new() { Score = score, SetsPlanned = planned ? 1 : 0 };

    [Fact]
    public void Streak_CountsGoodDaysInARow_SkipsEmptyDays_AndTodayOnlyAdds()
    {
        var days = new List<DayDTO> { Scored(90), Scored(10), Scored(70), Scored(0, planned: false), Scored(65), Scored(80), Scored(20) };

        var streak = DayService.Streak(days);

        Assert.Equal(3, streak.ThroughYesterday);
        Assert.Equal(3, streak.Current);
        Assert.Equal(20, streak.TodayScore);
        Assert.Equal(3, streak.Best);

        days[^1] = Scored(60);
        streak = DayService.Streak(days);
        Assert.Equal(4, streak.Current);
        Assert.Equal(4, streak.Best);
        Assert.Equal(60, streak.Threshold);
    }

    [Fact]
    public void Streak_BestRemembersAnEarlierLongerRun()
    {
        var days = new List<DayDTO> { Scored(90), Scored(90), Scored(90), Scored(90), Scored(10), Scored(90), Scored(0) };

        var streak = DayService.Streak(days);

        Assert.Equal(1, streak.ThroughYesterday);
        Assert.Equal(4, streak.Best);
    }

    [Fact]
    public void SetProgress_LoggingMarksTheDayTrained_ButOnlyLoggedSetsCount()
    {
        var day = WeekBuilder.Day(TestSupport.Tuesday, Plan, Logged(TrainingState.Done, false, 1, null), []);

        Assert.Equal(1, day.SetsDone);
        Assert.Equal(4, day.SetsPlanned);
    }
}
