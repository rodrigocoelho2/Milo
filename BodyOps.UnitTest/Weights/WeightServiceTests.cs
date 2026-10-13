using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.UnitTest.Weights;

public sealed class WeightServiceTests
{
    private readonly FakeWeightRepository _weights = new();
    private readonly FakeSettingRepository _settings = new();

    private WeightService Service() => new(_weights, _settings, new FakeClock(TestSupport.Tuesday), TestSupport.Globalization);

    [Fact]
    public async Task Save_RoundsToOneDecimal_ReplacesTheSameDate_AndListsWithTheGoal()
    {
        await new SettingService(_settings, TestSupport.Globalization).SaveAsync(1, new SettingsDTO { WeightGoalKg = 90 });
        await Service().SaveAsync(1, TestSupport.Tuesday, new WeightSaveDTO { WeightKg = 98.04m });

        var response = await Service().SaveAsync(1, TestSupport.Tuesday, new WeightSaveDTO { WeightKg = 97.96m });

        var entry = Assert.Single(response.Result!.Entries);
        Assert.Equal(98.0m, entry.WeightKg);
        Assert.Equal(90, response.Result.GoalKg);
    }

    [Fact]
    public async Task Save_FutureDateOrSillyWeight_IsAValidationProblem()
    {
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().SaveAsync(1, TestSupport.Tuesday.AddDays(1), new WeightSaveDTO { WeightKg = 90 })).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().SaveAsync(1, TestSupport.Tuesday, new WeightSaveDTO { WeightKg = 9 })).Status);
    }

    [Fact]
    public async Task Delete_Unknown_IsNotFound() =>
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await Service().DeleteAsync(1, TestSupport.Tuesday)).Status);

    [Fact]
    public async Task Goal_CanBeCleared_ButNotSilly()
    {
        var settings = new SettingService(_settings, TestSupport.Globalization);

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await settings.SaveAsync(1, new SettingsDTO { WeightGoalKg = 1000 })).Status);
        Assert.Null((await settings.SaveAsync(1, new SettingsDTO { WeightGoalKg = null })).Result!.WeightGoalKg);
    }
}
