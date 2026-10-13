using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Days;

public sealed class DayServiceTests
{
    private readonly FakePlanRepository _plan = new();
    private readonly FakeDayRepository _days;
    private readonly FakeClock _clock = new(TestSupport.Tuesday);

    public DayServiceTests()
    {
        _days = new FakeDayRepository(_plan);
        var plans = new PlanService(_plan, _clock, TestSupport.Globalization);
        plans.SaveMealsAsync(1, 2, [new PlanMealSaveDTO { Name = "Breakfast" }, new PlanMealSaveDTO { Name = "Lunch" }]).Wait();
        plans.SaveExercisesAsync(1, 2, [new PlanExerciseSaveDTO { Name = "Pec deck", Detail = "3x12" }]).Wait();
    }

    private DayService Service() => new(_days, _plan, _clock, TestSupport.Globalization);

    private long MealId(string name) => _plan.Meals.Single(meal => meal.Name == name).PlanMealID;

    private long ExerciseId => _plan.Exercises.Single().PlanExerciseID;

    [Fact]
    public async Task GetWeek_StartsOnMonday_AndKnowsToday()
    {
        var response = await Service().GetWeekAsync(1, null);

        Assert.Equal(TestSupport.Monday, response.Result!.From);
        Assert.Equal(TestSupport.Tuesday, response.Result.Today);
        Assert.Equal(7, response.Result.Days.Count);
    }

    [Fact]
    public async Task TickingAMeal_LogsTheDay_AndAnswersWithTheWeek()
    {
        var response = await Service().SetMealEatenAsync(1, TestSupport.Tuesday, MealId("Lunch"), new MealEatenDTO { Eaten = true });

        var tuesday = response.Result!.Days[1];
        Assert.True(tuesday.Logged);
        Assert.Equal([false, true], tuesday.Meals.Select(meal => meal.Eaten));
        Assert.Equal(1, response.Result.Totals.MealsEaten);
    }

    [Fact]
    public async Task TickingAMealThatIsNotOnThatDay_IsNotFound() =>
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await Service().SetMealEatenAsync(1, TestSupport.Monday, MealId("Lunch"), new MealEatenDTO { Eaten = true })).Status);

    [Fact]
    public async Task LoggingSets_MarksTheDayTrained()
    {
        var response = await Service().LogExerciseAsync(1, TestSupport.Tuesday, ExerciseId, new ExerciseLogDTO { LoadKg = 70, Sets = [12, 12, null] });

        var day = response.Result!.Days[1];
        Assert.Equal(TrainingState.Done, day.TrainingState);
        Assert.Equal([12, 12, null], day.Exercises[0].Sets);
        Assert.Equal(70 * 24, day.Exercises[0].VolumeKg);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1001, 10)]
    [InlineData(50, 1000)]
    public async Task LoggingOutOfRangeLoadOrReps_IsAValidationProblem(int load, int reps) =>
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems,
            (await Service().LogExerciseAsync(1, TestSupport.Tuesday, ExerciseId, new ExerciseLogDTO { LoadKg = load, Sets = [reps] })).Status);

    [Fact]
    public async Task DatesFarInTheFuture_AreRefused() =>
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().SetAllMealsEatenAsync(1, TestSupport.Tuesday.AddYears(2), new MealEatenDTO { Eaten = true })).Status);

    [Fact]
    public async Task ResetWeek_RemovesTheDays_AndHandsThemBackForUndo()
    {
        await Service().SetAllMealsEatenAsync(1, TestSupport.Tuesday, new MealEatenDTO { Eaten = true });

        var removed = await Service().ResetWeekAsync(1, TestSupport.Tuesday);

        Assert.Single(removed.Result!);
        Assert.All(removed.Result![0].Meals, meal => Assert.True(meal.Eaten));
        Assert.Empty(_days.Data.Days);

        await Service().RestoreWeekAsync(1, TestSupport.Tuesday, removed.Result);
        Assert.Single(_days.Imports);
    }

    [Fact]
    public async Task Consistency_CoversWholeWeeksUpToToday()
    {
        await Service().SetMealEatenAsync(1, TestSupport.Tuesday, MealId("Breakfast"), new MealEatenDTO { Eaten = true });

        var response = await Service().GetConsistencyAsync(1, 2);

        Assert.Equal(TestSupport.Monday.AddDays(-7), response.Result![0].Date);
        Assert.Equal(TestSupport.Tuesday, response.Result[^1].Date);
        Assert.Equal(0.5, response.Result[^1].Score);
        Assert.Null(response.Result[^2].Score);
    }
}
