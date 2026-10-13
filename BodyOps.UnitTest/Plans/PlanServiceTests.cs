using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Plans;

public sealed class PlanServiceTests
{
    private readonly FakePlanRepository _plan = new();

    private PlanService Service() => new(_plan, new FakeClock(TestSupport.Tuesday), TestSupport.Globalization);

    private static PlanMealSaveDTO Breakfast(long? id = null) => new()
    {
        Id = id,
        Name = "  Breakfast  ",
        Foods = [new PlanFoodSaveDTO { Name = "Whey", Amount = 25 }, new PlanFoodSaveDTO { Name = "Banana", Amount = 1, Unit = FoodUnit.Pieces }],
    };

    [Fact]
    public async Task Get_AlwaysHasSevenDays_MondayFirst()
    {
        var response = await Service().GetAsync(1);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7], response.Result!.Days.Select(day => day.Weekday));
    }

    [Fact]
    public async Task SaveMeals_CleansNames_KeepsOrder_AndSyncsFromThisMonday()
    {
        var response = await Service().SaveMealsAsync(1, 2, [Breakfast(), new PlanMealSaveDTO { Name = "Lunch" }]);

        Assert.Equal(ServiceApiResponseStatus.Success, response.Status);
        Assert.Equal(["Breakfast", "Lunch"], response.Result!.Meals.Select(meal => meal.Name));
        Assert.Equal(FoodUnit.Pieces, response.Result.Meals[0].Foods[1].Unit);
        Assert.Equal([TestSupport.Monday], _plan.SyncDates);
    }

    [Fact]
    public async Task SaveMeals_WithTheSameId_UpdatesInsteadOfAdding()
    {
        var first = await Service().SaveMealsAsync(1, 2, [Breakfast()]);
        var id = first.Result!.Meals[0].Id;

        var second = await Service().SaveMealsAsync(1, 2, [Breakfast(id), new PlanMealSaveDTO { Name = "Snack" }]);

        Assert.Equal(id, second.Result!.Meals[0].Id);
        Assert.Equal(2, _plan.Meals.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Save_WeekdayOutOfRange_IsAValidationProblem(int weekday)
    {
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().SaveMealsAsync(1, weekday, [])).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().SaveExercisesAsync(1, weekday, [])).Status);
    }

    [Fact]
    public async Task SaveMeals_EmptyNameOrNegativeAmount_IsAValidationProblem()
    {
        var noName = await Service().SaveMealsAsync(1, 1, [new PlanMealSaveDTO { Name = "   " }]);
        var negative = await Service().SaveMealsAsync(1, 1, [new PlanMealSaveDTO { Name = "Lunch", Foods = [new PlanFoodSaveDTO { Name = "Rice", Amount = -1 }] }]);

        Assert.Equal("meals[0].name", noName.Problems!.Errors[0].Field);
        Assert.Equal("meals[0].foods[0].amount", negative.Problems!.Errors[0].Field);
        Assert.Empty(_plan.Meals);
    }

    [Fact]
    public async Task SaveExercises_DetectsMusclesAndPlannedSets_UnlessPickedByHand()
    {
        var response = await Service().SaveExercisesAsync(1, 2,
        [
            new PlanExerciseSaveDTO { Name = "Pec deck", Detail = "3x12" },
            new PlanExerciseSaveDTO { Name = "Pec deck", Detail = "3x12", Muscles = [Muscle.Shoulders] },
            new PlanExerciseSaveDTO { Name = "Cycling", Detail = "20 km", Type = ExerciseType.Cardio },
        ]);

        var exercises = response.Result!.Exercises;
        Assert.True(exercises[0].MusclesAuto);
        Assert.Contains(Muscle.Chest, exercises[0].Muscles);
        Assert.Equal([12, 12, 12], exercises[0].Targets);
        Assert.False(exercises[1].MusclesAuto);
        Assert.Equal([Muscle.Shoulders], exercises[1].Muscles);
        Assert.Empty(exercises[2].Muscles);
        Assert.Empty(exercises[2].Targets);
    }

    [Fact]
    public async Task Copy_ToOtherDays_ReplacesTheirExercises()
    {
        await Service().SaveExercisesAsync(1, 1, [new PlanExerciseSaveDTO { Name = "Hack squat", Detail = "3x12" }]);
        await Service().SaveExercisesAsync(1, 4, [new PlanExerciseSaveDTO { Name = "Pec deck" }]);

        var response = await Service().CopyAsync(1, 1, new PlanCopyDTO { Part = PlanPart.Exercises, Targets = [1, 4, 5] });

        Assert.Equal(["Hack squat"], response.Result!.Days[3].Exercises.Select(exercise => exercise.Name));
        Assert.Equal(["Hack squat"], response.Result.Days[4].Exercises.Select(exercise => exercise.Name));
        Assert.Single(response.Result.Days[0].Exercises);
    }

    [Fact]
    public async Task Copy_OnlyToItself_IsAValidationProblem() =>
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().CopyAsync(1, 3, new PlanCopyDTO { Targets = [3] })).Status);

    [Fact]
    public async Task SaveMeals_KeepsTheTime_AndRejectsOneThatIsNotATimeOfDay()
    {
        var breakfast = Breakfast();
        breakfast.Time = "8:05";
        var response = await Service().SaveMealsAsync(1, 2, [breakfast, new PlanMealSaveDTO { Name = "Lunch", Time = " " }]);

        Assert.Equal("08:05", response.Result!.Meals[0].Time);
        Assert.Null(response.Result.Meals[1].Time);

        var bad = Breakfast();
        bad.Time = "25:00";
        var problem = await Service().SaveMealsAsync(1, 2, [bad]);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, problem.Status);
        Assert.Equal("meals[0].time", problem.Problems!.Errors[0].Field);
    }

    [Fact]
    public async Task SaveName_NamesTheDay_EmptyRemovesIt_AndCopyingExercisesCopiesIt()
    {
        var named = await Service().SaveNameAsync(1, 2, new PlanDayNameDTO { Name = "  Upper   body " });
        Assert.Equal("Upper body", named.Result!.Name);

        await Service().CopyAsync(1, 2, new PlanCopyDTO { Part = PlanPart.Exercises, Targets = [5] });
        var plan = (await Service().GetAsync(1)).Result!;
        Assert.Equal("Upper body", plan.Days[4].Name);

        var cleared = await Service().SaveNameAsync(1, 2, new PlanDayNameDTO { Name = "" });
        Assert.Equal(string.Empty, cleared.Result!.Name);

        var tooLong = await Service().SaveNameAsync(1, 2, new PlanDayNameDTO { Name = new string('x', PlanService.MaxDayName + 1) });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, tooLong.Status);
    }
}
