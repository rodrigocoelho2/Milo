using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Exercises.Services;

namespace BodyOps.UnitTest.Exercises;

public sealed class RecordTests
{
    private static ExerciseLogRow Row(int month, int day, string name, decimal? load) =>
        new() { DayDate = new DateTime(2026, month, day), NameKey = name.ToLowerInvariant(), Name = name, LoadKg = load, Reps = "10,10,10" };

    [Fact]
    public void Records_AreLoadsThatBeatEveryEarlierOne_TheFirstSessionIsNot()
    {
        var records = ExerciseService.Records(
        [
            Row(7, 14, "Incline chest press", 95),
            Row(7, 21, "Incline chest press", 100),
            Row(7, 28, "Incline chest press", 100),
            Row(8, 4, "Incline chest press", 97.5m),
            Row(10, 13, "Incline chest press", 102.5m),
            Row(7, 14, "Pec deck", 70),
            Row(7, 21, "Pec deck", null),
        ]);

        Assert.Equal(2, records.Count);
        Assert.Equal((new DateOnly(2026, 7, 21), 100m, 95m), (records[0].Date, records[0].LoadKg, records[0].PreviousKg));
        Assert.Equal((new DateOnly(2026, 10, 13), 102.5m, 100m), (records[1].Date, records[1].LoadKg, records[1].PreviousKg));
    }

    [Fact]
    public async Task List_HasTheBestDateAndATrend()
    {
        var plan = new FakePlanRepository();
        var days = new FakeDayRepository(plan);
        var service = new ExerciseService(days, plan, TestSupport.Globalization);

        plan.Exercises.Add(new() { PlanExerciseID = 10, Weekday = 2, ExerciseType = BodyOps.Library.Enums.ExerciseType.Strength, Name = "Pec deck", Detail = "3x12" });
        await days.LogExerciseAsync(1, TestSupport.Tuesday.AddDays(-7), 10, 65, [12, 12, 12], [], null, null);
        await days.LogExerciseAsync(1, TestSupport.Tuesday, 10, 70, [12, 12], [], null, null);

        var summary = Assert.Single((await service.ListAsync(1)).Result!);
        Assert.Equal(70, summary.BestKg);
        Assert.Equal(TestSupport.Tuesday, summary.BestDate);
        Assert.Equal([65m, 70m], summary.Trend.Select(point => point.LoadKg));

        var records = (await service.RecordsAsync(1, 10)).Result!;
        Assert.Equal(70, Assert.Single(records).LoadKg);
    }
}
