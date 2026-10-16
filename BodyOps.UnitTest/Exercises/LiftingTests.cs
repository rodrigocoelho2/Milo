using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Exercises.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Exercises;

public sealed class LiftingTests
{
    [Theory]
    [InlineData(70, 12, 98)]
    [InlineData(75, 8, 95)]
    [InlineData(100, 1, 100)]
    [InlineData(65, 10, 86.7)]
    public void E1rm_IsEpley(double load, int reps, double expected) =>
        Assert.Equal((decimal)expected, Lifting.E1rm((decimal)load, reps));

    [Theory]
    [InlineData(70, 13)]
    [InlineData(70, 0)]
    [InlineData(0, 10)]
    public void E1rm_LeavesOutSetsItCantEstimate(double load, int reps) =>
        Assert.Null(Lifting.E1rm((decimal)load, reps));

    [Fact]
    public void BestE1rm_PicksTheBestSet_IgnoringSetsNotDone() =>
        Assert.Equal(98m, Lifting.BestE1rm([12, null, 6], [70m, 90m, 80m]));

    [Fact]
    public void RepBests_KeepTheHeaviestLoadForAtLeastEachCount()
    {
        var bests = Lifting.RepBests([(12, 70m), (8, 75m), (5, 80m), (15, null)]);

        Assert.Equal(80m, bests[3]);
        Assert.Equal(80m, bests[5]);
        Assert.Equal(75m, bests[8]);
        Assert.Equal(70m, bests[10]);
        Assert.Equal(70m, bests[12]);
        Assert.False(bests.ContainsKey(15));
    }

    [Fact]
    public void RepRecord_IsTheHighestCountBeaten_AndAFirstTimeIsNotOne()
    {
        var before = new Dictionary<int, decimal> { [3] = 85, [5] = 80, [8] = 75, [10] = 70, [12] = 67.5m };

        Assert.Equal((12, 67.5m, 70m), Lifting.RepRecord(before, Lifting.RepBests([(12, 70m), (12, 70m)])));
        Assert.Null(Lifting.RepRecord(before, Lifting.RepBests([(15, 50m)])));
        Assert.Null(Lifting.RepRecord(before, Lifting.RepBests([(10, 70m)])));
    }

    [Fact]
    public void Earned_NeedsARange_AndEverySetAtTheTop()
    {
        var range = ExerciseDetailParser.Sets("3x8-12");

        Assert.True(Lifting.Earned(range, [12, 12, 12]));
        Assert.True(Lifting.Earned(range, [12, 13, 12, 8]));
        Assert.False(Lifting.Earned(range, [12, 12, 11]));
        Assert.False(Lifting.Earned(range, [12, 12]));
        Assert.False(Lifting.Earned(range, [12, null, 12]));
        Assert.False(Lifting.Earned(ExerciseDetailParser.Sets("3x12"), [12, 12, 12]));
    }

    [Fact]
    public void Week_RangePlan_ShowsTheStep_TheEarnedLastSession_AndRepRecords()
    {
        var plan = new PlanData([], [], [new PlanExercise { PlanExerciseID = 10, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Incline chest press", Detail = "3x8-12" }]);
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = TestSupport.Tuesday.ToDateTime(TimeOnly.MinValue), TrainingState = TrainingState.Done }],
            [],
            [new DayExerciseRow { DayID = 5, DayExerciseID = 50, PlanExerciseID = 10, ExerciseType = ExerciseType.Strength, Name = "Incline chest press", NameKey = "incline chest press", Detail = "3x8-12", LoadKg = 70 }],
            [
                new DaySetRow { DayExerciseID = 50, SetNumber = 1, Reps = 12 },
                new DaySetRow { DayExerciseID = 50, SetNumber = 2, Reps = 12 },
                new DaySetRow { DayExerciseID = 50, SetNumber = 3, Reps = 12 },
            ]);
        ExerciseLogRow[] history =
        [
            new() { DayDate = new DateTime(2026, 7, 7), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 67.5m, Reps = "12,12,12" },
            new() { DayDate = new DateTime(2026, 7, 14), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 70, Reps = "11,10,10" },
            new() { DayDate = TestSupport.Tuesday.ToDateTime(TimeOnly.MinValue), NameKey = "incline chest press", Name = "Incline chest press", LoadKg = 70, Reps = "12,12,12" },
        ];

        var tuesday = WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, plan, logged, history).Days[1].Exercises[0];
        Assert.Equal([12, 12, 12], tuesday.Targets);
        Assert.Equal([8, 8, 8], tuesday.TargetsMin);
        Assert.Equal(Lifting.DefaultStepKg, tuesday.StepKg);
        Assert.True(tuesday.Earned);
        Assert.False(tuesday.Last!.Earned);
        Assert.Equal(98m, tuesday.E1rmKg);
        Assert.Equal(95.7m, tuesday.BestE1rmBeforeKg);
        Assert.Equal((12, 67.5m, 70m), (tuesday.RepRecord!.Reps, tuesday.RepRecord.PreviousKg, tuesday.RepRecord.LoadKg));

        // A week later, unlogged: the last session earned the step.
        var next = WeekBuilder.Week(TestSupport.Monday.AddDays(7), TestSupport.Tuesday.AddDays(7), plan, DayRange.Empty, history).Days[1].Exercises[0];
        Assert.True(next.Last!.Earned);
        Assert.Equal([70m, 70m, 70m], next.Last.Loads);
    }

    [Fact]
    public void Week_FixedRepsPlan_HasNoStep()
    {
        var plan = new PlanData([], [], [new PlanExercise { PlanExerciseID = 10, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Pec deck", Detail = "3x12", ProgressStepKg = 5 }]);

        var exercise = WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, plan, DayRange.Empty, []).Days[1].Exercises[0];
        Assert.Null(exercise.StepKg);
        Assert.Equal([12, 12, 12], exercise.TargetsMin);
    }

    [Fact]
    public void Records_OnePerSession_LoadThenRepsThenE1rm()
    {
        static ExerciseLogRow Row(int day, decimal load, string reps) =>
            new() { DayDate = new DateTime(2026, 7, day), NameKey = "press", Name = "Press", LoadKg = load, Reps = reps };

        var records = ExerciseService.Records(
        [
            Row(1, 67.5m, "12,12,12"),
            Row(8, 70, "11,10,10"),  // heavier: a load record
            Row(15, 70, "12,12,12"), // same load, 12 reps at 70 beats 67.5: a 12-rep record
            Row(22, 65, "12,12,12"), // nothing
        ]);

        Assert.Equal(2, records.Count);
        Assert.Equal(RecordKind.Load, records[0].Kind);
        Assert.Equal((RecordKind.Reps, 12, 70m, 67.5m), (records[1].Kind, records[1].Reps, records[1].LoadKg, records[1].PreviousKg));
    }

    [Fact]
    public void Records_E1rmBeatenWithoutAnyOtherRecord()
    {
        var records = ExerciseService.Records(
        [
            new() { DayDate = new DateTime(2026, 7, 1), NameKey = "press", Name = "Press", LoadKg = 80, Reps = "5,5,5" },   // e1RM 93.3
            new() { DayDate = new DateTime(2026, 7, 8), NameKey = "press", Name = "Press", LoadKg = 70, Reps = "11,11" },  // e1RM 95.7
        ]);

        var record = Assert.Single(records);
        Assert.Equal((RecordKind.E1rm, 95.7m, 93.3m), (record.Kind, record.LoadKg, record.PreviousKg));
    }

    [Fact]
    public async Task History_HasE1rmPerSessionAndRepRecords()
    {
        var plan = new FakePlanRepository();
        var days = new FakeDayRepository(plan);
        var service = new ExerciseService(days, plan, TestSupport.Globalization);
        plan.Exercises.Add(new() { PlanExerciseID = 10, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Press", Detail = "3x8-12" });
        await days.LogExerciseAsync(1, TestSupport.Tuesday.AddDays(-7), 10, 75, [8, 8, 8], [], null, null);
        await days.LogExerciseAsync(1, TestSupport.Tuesday, 10, 70, [12, 12, 12], [], null, null);

        var history = (await service.HistoryAsync(1, "Press")).Result!;
        Assert.Equal([95m, 98m], history.Sessions.Select(session => session.E1rmKg));
        Assert.Equal(98m, history.BestE1rmKg);
        Assert.Equal(75m, history.RepRecords.Single(best => best.Reps == 8).LoadKg);
        Assert.Equal(TestSupport.Tuesday, history.RepRecords.Single(best => best.Reps == 12).Date);

        var summary = Assert.Single((await service.ListAsync(1)).Result!);
        Assert.Equal((98m, 70m, 12), (summary.BestE1rmKg, summary.BestE1rmLoadKg, summary.BestE1rmReps));
    }

    [Fact]
    public async Task Plan_SavesTheStep_AndRejectsOneOutOfRange()
    {
        var repository = new FakePlanRepository();
        var service = new PlanService(repository, new FakeClock(TestSupport.Tuesday), TestSupport.Globalization);

        var saved = await service.SaveExercisesAsync(1, 2, [new PlanExerciseSaveDTO { Name = "Press", Detail = "3x8-12", ProgressStepKg = 5 }]);
        var exercise = Assert.Single(saved.Result!.Exercises);
        Assert.Equal(5m, exercise.StepKg);
        Assert.Equal([8, 8, 8], exercise.TargetsMin);

        var bad = await service.SaveExercisesAsync(1, 2, [new PlanExerciseSaveDTO { Name = "Press", Detail = "3x8-12", ProgressStepKg = 50 }]);
        Assert.False(bad.IsSuccess);
    }
}
