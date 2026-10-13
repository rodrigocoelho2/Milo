using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Models.Entities;

namespace BodyOps.UnitTest.Days;

public sealed class WeekBuilderTests
{
    private static readonly PlanData Plan = new(
        [new PlanMeal { PlanMealID = 1, Weekday = 2, Name = "Breakfast", SortOrder = 0 }],
        [
            new PlanFood { PlanFoodID = 1, PlanMealID = 1, Name = "Whey", Amount = 25, Unit = FoodUnit.Grams, SortOrder = 0 },
            new PlanFood { PlanFoodID = 2, PlanMealID = 1, Name = "Banana", Amount = 1, Unit = FoodUnit.Pieces, SortOrder = 1 },
        ],
        [new PlanExercise { PlanExerciseID = 10, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Incline chest press", Detail = "3x12" }]);

    private static DateTime At(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    [Fact]
    public void Week_UnloggedDay_ShowsTheCurrentPlan_WithNothingDone()
    {
        var week = WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, Plan, DayRange.Empty, []);

        var tuesday = week.Days[1];
        Assert.False(tuesday.Logged);
        Assert.Equal("Whey 25 g · Banana 1 pc", tuesday.Meals[0].Foods);
        Assert.Equal([null, null, null], tuesday.Exercises[0].Sets);
        Assert.Equal(1, week.Totals.MealsPlanned);
        Assert.Equal(1, week.Totals.TrainingDaysPlanned);
        Assert.Empty(week.Days[0].Meals);
    }

    [Fact]
    public void Week_LoggedDay_ShowsItsOwnCopy_WithSetsVolumeLastSessionAndRecord()
    {
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = At(TestSupport.Tuesday), TrainingState = TrainingState.Done }],
            [new DayMealRow { DayID = 5, PlanMealID = 1, Name = "Breakfast (old)", Foods = "Oats 50 g", Eaten = true }],
            [new DayExerciseRow { DayID = 5, DayExerciseID = 50, PlanExerciseID = 10, ExerciseType = ExerciseType.Strength, Name = "Incline chest press", NameKey = "incline chest press", Detail = "3x12", LoadKg = 102.5m }],
            [new DaySetRow { DayExerciseID = 50, SetNumber = 1, Reps = 12 }, new DaySetRow { DayExerciseID = 50, SetNumber = 2, Reps = 11 }]);
        ExerciseLogRow[] history =
        [
            new() { DayDate = new DateTime(2026, 7, 14), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 95, Reps = "10,10,10" },
            new() { DayDate = new DateTime(2026, 7, 21), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 100, Reps = "10,10,10" },
            new() { DayDate = At(TestSupport.Tuesday), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 102.5m, Reps = "12,11" },
        ];

        var week = WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, Plan, logged, history);

        var day = week.Days[1];
        Assert.True(day.Logged);
        Assert.Equal("Breakfast (old)", day.Meals[0].Name);
        var exercise = day.Exercises[0];
        Assert.Equal([12, 11, null], exercise.Sets);
        Assert.Equal(102.5m * 23, exercise.VolumeKg);
        Assert.Equal(new DateOnly(2026, 7, 21), exercise.Last!.Date);
        Assert.Equal([10, 10, 10], exercise.Last.Reps);
        Assert.Equal(100, exercise.BestBeforeKg);
        Assert.True(exercise.IsPr);
        Assert.Equal(1, week.Totals.TrainedDays);
        Assert.Equal(2, week.Totals.SetsLogged);
        Assert.Equal(Muscle.Chest, week.Totals.Muscles[0].Muscle);
        Assert.Equal(2, week.Totals.Muscles[0].Sets);
    }

    [Fact]
    public void Week_SameLoadAsBefore_IsNotARecord()
    {
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = At(TestSupport.Tuesday) }],
            [],
            [new DayExerciseRow { DayID = 5, DayExerciseID = 50, PlanExerciseID = 10, ExerciseType = ExerciseType.Strength, Name = "Incline chest press", NameKey = "incline chest press", LoadKg = 100 }],
            []);
        ExerciseLogRow[] history = [new() { DayDate = new DateTime(2026, 7, 21), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 100 }];

        Assert.False(WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, Plan, logged, history).Days[1].Exercises[0].IsPr);
    }

    [Fact]
    public void Score_AveragesMealsAndSession_AndIsNullWhenNothingLogged()
    {
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = At(TestSupport.Tuesday), TrainingState = TrainingState.Missed }],
            [
                new DayMealRow { DayID = 5, PlanMealID = 1, Name = "Breakfast", Eaten = true },
                new DayMealRow { DayID = 5, PlanMealID = 2, Name = "Lunch", Eaten = false },
            ],
            [],
            []);

        Assert.Equal(0.25, WeekBuilder.Score(WeekBuilder.Day(TestSupport.Tuesday, Plan, logged, [])));
        Assert.Null(WeekBuilder.Score(WeekBuilder.Day(TestSupport.Monday, Plan, logged, [])));
    }

    [Fact]
    public void FoodsLine_WritesAmountsLikeTheDatabaseView() =>
        Assert.Equal("Rice 12.5 g · Egg 2 pc", WeekBuilder.FoodsLine(
        [
            new PlanFood { Name = "Rice", Amount = 12.50m, Unit = FoodUnit.Grams, SortOrder = 0 },
            new PlanFood { Name = "Egg", Amount = 2.00m, Unit = FoodUnit.Pieces, SortOrder = 1 },
        ]));
}
