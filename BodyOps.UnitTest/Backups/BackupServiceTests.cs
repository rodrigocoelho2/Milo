using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Backups.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Backups;

public sealed class BackupServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePlanRepository _plan = new();
    private readonly FakeDayRepository _days;
    private readonly FakeWeightRepository _weights = new();
    private readonly FakeSettingRepository _settings = new();
    private readonly FakeClock _clock = new(TestSupport.Tuesday);

    public BackupServiceTests() => _days = new FakeDayRepository(_plan);

    private BackupService Service() => new(_users, _plan, _days, _weights, _settings, _clock, TimeProvider.System, TestSupport.Globalization, TestSupport.Options());

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Import_CorpoOpsBackup_ReplacesTheAccountsData_AndLinksDaysToTheNewPlan()
    {
        var file = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Backups", "corpoops-sample.json"));

        var response = await Service().ImportAsync(1, Json(file));

        Assert.Equal(ServiceApiResponseStatus.Success, response.Status);
        Assert.Equal("corpoops", response.Result!.Source);
        Assert.Equal(2, response.Result.Meals);
        Assert.Equal(3, response.Result.Exercises);
        Assert.Equal(2, response.Result.Days);
        Assert.Equal(2, response.Result.Weights);
        Assert.Equal([1L], _users.Cleared);
        Assert.Equal(90, _settings.Setting!.WeightGoalKg);

        using var days = JsonDocument.Parse(Assert.Single(_days.Imports));
        var tuesday = days.RootElement[0];
        var pressId = tuesday.GetProperty("exercises")[0].GetProperty("planExerciseId").GetInt64();
        Assert.Equal(_plan.Exercises.Single(exercise => exercise.Name == "Chest Press Inclinada").PlanExerciseID, pressId);

        // "Face Pull" was no longer planned: it keeps its history under a negative id.
        Assert.True(tuesday.GetProperty("exercises")[2].GetProperty("planExerciseId").GetInt64() < 0);
    }

    [Fact]
    public async Task ExportThenImport_GivesTheSamePlan()
    {
        var plans = new PlanService(_plan, _clock, TestSupport.Globalization);
        await plans.SaveExercisesAsync(1, 2, [new PlanExerciseSaveDTO { Name = "Pec deck", Detail = "3x12" }]);
        await plans.SaveMealsAsync(1, 2, [new PlanMealSaveDTO { Name = "Breakfast", Foods = [new PlanFoodSaveDTO { Name = "Whey", Amount = 25 }] }]);
        await _weights.SaveAsync(1, new DateOnly(2026, 10, 1), 97.5m);

        var exported = await Service().ExportAsync(1);
        var json = JsonSerializer.Serialize(exported.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _plan.Meals.Clear();
        _plan.Foods.Clear();
        _plan.Exercises.Clear();

        var imported = await Service().ImportAsync(1, Json(json));

        Assert.Equal("bodyops", imported.Result!.Source);
        Assert.Equal("Pec deck", _plan.Exercises.Single().Name);
        Assert.Equal("Whey", _plan.Foods.Single().Name);
        Assert.Equal(97.5m, _weights.Entries[new DateOnly(2026, 10, 1)]);
    }

    [Fact]
    public async Task ExportThenImport_KeepsEachSetsLoad_AndTheKindOfCardio()
    {
        var plans = new PlanService(_plan, _clock, TestSupport.Globalization);
        await plans.SaveExercisesAsync(1, 2,
        [
            new PlanExerciseSaveDTO { Name = "Leg press", Detail = "3x10" },
            new PlanExerciseSaveDTO { Type = ExerciseType.Cardio, Name = "Cardio", Detail = "20 min", CardioKind = CardioKind.Treadmill },
        ]);
        var days = new DayService(_days, _plan, _weights, _clock, TestSupport.Globalization);
        var press = _plan.Exercises.Single(exercise => exercise.Name == "Leg press").PlanExerciseID;
        await days.LogExerciseAsync(1, TestSupport.Tuesday, press, new ExerciseLogDTO { Sets = [12, 10, 8], SetLoads = [100, 110, 120] });

        var exported = await Service().ExportAsync(1);
        var json = JsonSerializer.Serialize(exported.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Service().ImportAsync(1, Json(json));

        Assert.Equal(CardioKind.Treadmill, _plan.Exercises.Single(exercise => exercise.Name == "Cardio").CardioKind);
        using var imported = JsonDocument.Parse(Assert.Single(_days.Imports));
        var exercises = imported.RootElement[0].GetProperty("exercises");
        Assert.Equal([100m, 110m, 120m], exercises[0].GetProperty("setLoads").EnumerateArray().Select(load => load.GetDecimal()));
        Assert.Equal((int)CardioKind.Treadmill, exercises[1].GetProperty("cardioKind").GetInt32());
    }

    [Theory]
    [InlineData("""{"hello":"world"}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""{"format":"bodyops-backup","plan":"not a list"}""")]
    public async Task Import_SomethingElse_IsRefused_AndChangesNothing(string json)
    {
        var response = await Service().ImportAsync(1, Json(json));

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, response.Status);
        Assert.Equal("This file is not a Milo (BodyOps) or CorpoOps backup.", response.Problems!.Errors[0].Message);
        Assert.Empty(_users.Cleared);
    }
}
