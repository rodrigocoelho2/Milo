using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Watch.Services;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.UnitTest.Watch;

/// <summary>
/// Apple Watch and Apple Health through the Shortcut: reading what Shortcuts sends, the key, matching the planned cardio.
/// </summary>
public sealed class WatchTests
{
    private static readonly DateOnly Today = TestSupport.Tuesday;
    private static readonly DateTime Now = Today.ToDateTime(new TimeOnly(19, 30));
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    [Theory]
    [InlineData("412", 412)]
    [InlineData("412 kcal", 412)]
    [InlineData("82,4 kg", 82.4)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234", 1234)]
    [InlineData("  58 min", 58)]
    public void Number_ReadsWhatShortcutsWrites(string text, double expected) =>
        Assert.Equal((decimal)expected, WatchPayload.Number(text));

    [Theory]
    [InlineData("\"58 min\"", null, 3480)]
    [InlineData("\"1 hr 2 min\"", null, 3720)]
    [InlineData("\"58m 12s\"", null, 3492)]
    [InlineData("\"0:58:12\"", null, 3492)]
    [InlineData("\"58:12\"", null, 3492)]
    [InlineData("\"1:05\"", null, 3900)]
    [InlineData("3492", null, 3492)]
    [InlineData("58", null, 3480)]
    [InlineData("58", "seconds", 58)]
    [InlineData("\"58,5\"", "minutes", 3510)]
    public void Duration_ReadsSecondsMinutesAndClocks(string json, string? unit, int expected) =>
        Assert.Equal(expected, WatchPayload.Duration(JsonDocument.Parse(json).RootElement, unit));

    [Theory]
    [InlineData("2026-10-13T18:05:00+01:00", 18, 5)]
    [InlineData("2026-10-13T17:05:00Z", 18, 5)]
    [InlineData("2026-10-13T18:05:00", 18, 5)]
    [InlineData("13 Oct 2026 at 18:05", 18, 5)]
    [InlineData("13/10/2026, 18:05", 18, 5)]
    [InlineData("Oct 13, 2026 at 6:05 PM", 18, 5)]
    [InlineData("13 de outubro de 2026 às 18:05", 18, 5)]
    public void Date_ReadsIsoAndThePhonesOwnFormat(string text, int hour, int minute)
    {
        var date = WatchPayload.Date(JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement, Lisbon, Now);

        Assert.Equal(Today.ToDateTime(new TimeOnly(hour, minute)), date);
    }

    [Fact]
    public void Date_DayAndMonthEitherWay_TakesTheReadingNearNow()
    {
        // 05/10 is 5 October (a week ago), not 10 May.
        var date = WatchPayload.Date(JsonDocument.Parse("\"05/10/2026 18:00\"").RootElement, Lisbon, Now);

        Assert.Equal(new DateTime(2026, 10, 5, 18, 0, 0), date);
    }

    [Fact]
    public void Parse_AFullWorkout_FromAShortcut()
    {
        var body = Json("""
            { "workout": { "Type": "Traditional Strength Training", "Start Date": "13 Oct 2026 at 18:14", "End Date": "13 Oct 2026 at 19:12",
              "Active Energy": "412 kcal", "Heart Rate": "120\n128\n140\n164\n150" } }
            """);

        var data = WatchPayload.Parse(body, Lisbon, Now);

        var workout = Assert.IsType<WatchWorkoutData>(data.Workout);
        Assert.Null(data.Error);
        Assert.Equal(("Traditional Strength Training", 58 * 60, 412m), (workout.Kind, workout.DurationSeconds, workout.ActiveKcal));
        Assert.Equal(Today.ToDateTime(new TimeOnly(18, 14)), workout.StartedAt);
        Assert.Equal((140, 164), (workout.AvgBpm, workout.MaxBpm));
        Assert.Equal([120, 128, 140, 164, 150], workout.HeartRates);
        Assert.Equal(WatchCategory.Strength, WatchPayload.Category(workout.Kind));
    }

    [Fact]
    public void Parse_OnlyADuration_EndsNow_AndAWeightComesWithItsDate()
    {
        var data = WatchPayload.Parse(Json("""{ "type": "Indoor Walk", "duration": "22 min", "distance": "1 850 m", "weight": { "value": "82,4 kg", "date": "2026-10-12" } }"""), Lisbon, Now);

        Assert.Equal(Now.AddMinutes(-22), data.Workout!.StartedAt);
        Assert.Equal(1.85m, data.Workout.DistanceKm);
        Assert.Equal(new WatchWeightData(82.4m, Today.AddDays(-1)), data.Weight);
    }

    [Fact]
    public void Parse_AWeightAlone_InPounds()
    {
        var data = WatchPayload.Parse(Json("""{ "weight": "181.7 lb", "date": "13/10/2026" }"""), Lisbon, Now);

        Assert.Null(data.Workout);
        Assert.Equal(new WatchWeightData(82.4m, Today), data.Weight);
    }

    [Fact]
    public void Parse_TheFreeShortcutsRoute_StartFromTheFile_EndNow()
    {
        var data = WatchPayload.Parse(Json("""
            { "type": "Strength", "start": "2026-10-13T18:14:05+01:00", "end": "13 Oct 2026 at 19:12", "activeEnergy": 412.6, "heartRate": [120, 128, 140] }
            """), Lisbon, Now);

        var workout = Assert.IsType<WatchWorkoutData>(data.Workout);
        Assert.Equal((Today.ToDateTime(new TimeOnly(18, 14)), (58 * 60) - 5), (workout.StartedAt, workout.DurationSeconds));
        Assert.Equal((412.6m, 129, 140), (workout.ActiveKcal!.Value, workout.AvgBpm!.Value, workout.MaxBpm!.Value));
        Assert.Equal(WatchCategory.Strength, WatchPayload.Category(workout.Kind));
    }

    [Fact]
    public void Parse_HealthAutoExport_WorkoutsAndWeight()
    {
        var data = WatchPayload.Parse(Json("""
            { "data": {
              "workouts": [
                { "id": "a", "name": "Traditional Strength Training", "start": "2026-10-13 18:14:00 +0100", "end": "2026-10-13 19:12:00 +0100", "duration": 3480,
                  "activeEnergyBurned": { "qty": 1724, "units": "kJ" }, "heartRate": { "min": { "qty": 90, "units": "bpm" }, "avg": { "qty": 128, "units": "bpm" }, "max": { "qty": 164, "units": "bpm" } },
                  "heartRateData": [ { "date": "2026-10-13 18:15:00 +0100", "Min": 100, "Avg": 110, "Max": 120 }, { "date": "2026-10-13 18:16:00 +0100", "Min": 130, "Avg": 140, "Max": 150 } ] },
                { "id": "b", "name": "Walking", "isIndoor": true, "start": "2026-10-13 19:14:00 +0100", "end": "2026-10-13 19:36:00 +0100", "duration": 1320,
                  "activeEnergyBurned": { "qty": 186, "units": "kcal" }, "distance": { "qty": 1.9, "units": "km" } }
              ],
              "metrics": [ { "name": "weight_body_mass", "units": "kg", "data": [ { "qty": 82.6, "date": "2026-10-12 08:01:00 +0100" }, { "qty": 82.4, "date": "2026-10-13 08:01:00 +0100" } ] },
                           { "name": "heart_rate", "units": "bpm", "data": [ { "date": "2026-10-13 08:00:00 +0100", "Min": 60, "Avg": 65, "Max": 70 } ] } ] } }
            """), Lisbon, Now);

        Assert.Null(data.Error);
        Assert.Equal(2, data.Workouts.Count);
        var strength = data.Workouts[0];
        Assert.Equal((58 * 60, 412m, 128, 164), (strength.DurationSeconds, Math.Round(strength.ActiveKcal!.Value), strength.AvgBpm!.Value, strength.MaxBpm!.Value));
        Assert.Equal([110, 140], strength.HeartRates);
        Assert.Equal(Today.ToDateTime(new TimeOnly(18, 14)), strength.StartedAt);
        var walk = data.Workouts[1];
        Assert.Equal(("Indoor Walking", 1.9m), (walk.Kind, walk.DistanceKm!.Value));
        Assert.Equal(CardioKind.Treadmill, WatchPayload.CardioKinds(walk.Kind)[0]);
        Assert.Equal([new WatchWeightData(82.6m, Today.AddDays(-1)), new WatchWeightData(82.4m, Today)], data.Weights);
    }

    [Fact]
    public void Parse_Nonsense_SaysWhatIsMissing()
    {
        Assert.NotNull(WatchPayload.Parse(Json("""{ "hello": 1 }"""), Lisbon, Now).Error);
        Assert.NotNull(WatchPayload.Parse(Json("""{ "type": "Run" }"""), Lisbon, Now).Error);
        Assert.NotNull(WatchPayload.Parse(Json("""{ "type": "Run", "duration": "30 min", "end": "2026-10-14T19:00:00" }"""), Lisbon, Now).Error);   // tomorrow
        Assert.NotNull(WatchPayload.Parse(Json("[1,2]"), Lisbon, Now).Error);
    }

    [Fact]
    public void Curve_KeepsAtMostSixtyPoints()
    {
        var curve = WatchPayload.Curve([.. Enumerable.Range(0, 600).Select(static i => 100 + (i % 60))]);

        Assert.Equal(60, curve.Count);
        Assert.All(curve, static bpm => Assert.InRange(bpm, 100, 160));
    }

    [Theory]
    [InlineData("Indoor Walk", new[] { CardioKind.Treadmill, CardioKind.Walk })]
    [InlineData("Outdoor Walk", new[] { CardioKind.Walk, CardioKind.Treadmill })]
    [InlineData("Corrida interior", new[] { CardioKind.Treadmill, CardioKind.Run })]
    [InlineData("Indoor Cycle", new[] { CardioKind.Bike })]
    [InlineData("Elliptical", new[] { CardioKind.Elliptical })]
    [InlineData("Traditional Strength Training", new CardioKind[0])]
    public void CardioKinds_BestMatchFirst(string kind, CardioKind[] expected) =>
        Assert.Equal(expected, WatchPayload.CardioKinds(kind));

    [Fact]
    public async Task Key_IsShownOnce_OnlyItsHashIsKept_AndANewOneReplacesIt()
    {
        var (service, watch, _, _) = Service();

        var first = (await service.CreateKeyAsync(1)).Result!;
        Assert.StartsWith(WatchService.KeyPrefix, first.Key);
        Assert.Equal(40, first.Key.Length);
        Assert.True(first.Watch.Connected);
        Assert.Equal(WatchService.Hint(first.Key), first.Watch.KeyHint);
        Assert.DoesNotContain(first.Key, first.Watch.KeyHint);

        var second = (await service.CreateKeyAsync(1)).Result!;
        Assert.Equal(ServiceApiResponseStatus.Unauthorized, (await service.IngestAsync(first.Key, Json("""{ "weight": 80 }"""))).Status);
        Assert.Equal(ServiceApiResponseStatus.Success, (await service.IngestAsync(second.Key, Json("""{ "weight": 80 }"""))).Status);

        await service.RevokeKeyAsync(1);
        Assert.Empty(watch.Keys);
        Assert.Equal(ServiceApiResponseStatus.Unauthorized, (await service.IngestAsync(second.Key, Json("""{ "weight": 80 }"""))).Status);
        Assert.Equal(ServiceApiResponseStatus.Unauthorized, (await service.IngestAsync(null, Json("""{ "weight": 80 }"""))).Status);
    }

    [Fact]
    public async Task IndoorWalk_FillsInThePlannedTreadmill_Once_AndKeepsWhatWasLogged()
    {
        var (service, watch, plan, _) = Service();
        plan.Exercises.Add(new PlanExercise { PlanExerciseID = 31, Weekday = 2, ExerciseType = ExerciseType.Strength, Name = "Bench press", Detail = "3x10" });
        plan.Exercises.Add(new PlanExercise { PlanExerciseID = 32, Weekday = 2, ExerciseType = ExerciseType.Cardio, Name = "Passadeira", Detail = "20 min", CardioKind = CardioKind.Treadmill, SortOrder = 1 });
        var key = (await service.CreateKeyAsync(1)).Result!.Key;
        var walk = Json("""{ "key": "KEY", "type": "Indoor Walk", "start": "2026-10-13T19:00:00", "end": "2026-10-13T19:22:00", "activeEnergy": 186 }""".Replace("KEY", key, StringComparison.Ordinal));

        var result = (await service.IngestAsync(null, walk)).Result!;

        Assert.Equal("Passadeira, 22 min", result.FilledCardio);
        Assert.Equal(32, result.Workout!.PlanExerciseId);
        Assert.Contains("Milo got Indoor Walk: 22 min · 186 kcal.", result.Message);
        var state = (await service.GetAsync(1)).Result!;
        Assert.Equal("cardio", Assert.Single(state.Workouts).Category);
        Assert.NotNull(state.LastSyncAt);

        // Sent again: one workout, the treadmill isn't touched again.
        var again = (await service.IngestAsync(null, walk)).Result!;
        Assert.Null(again.FilledCardio);
        Assert.Single(watch.Workouts);
        Assert.Equal(32, watch.Workouts[0].PlanExerciseID);
    }

    [Fact]
    public async Task Weight_IsSaved_UnlessThatDayAlreadyHasOne()
    {
        var (service, _, _, weights) = Service();
        var key = (await service.CreateKeyAsync(1)).Result!.Key;

        var saved = (await service.IngestAsync(key, Json("""{ "weight": "82.4 kg", "date": "2026-10-13T07:58:00" }"""))).Result!;
        Assert.Equal(82.4m, saved.WeightKg);
        Assert.Equal(82.4m, Assert.Single(weights.Entries).Value);

        var kept = (await service.IngestAsync(key, Json("""{ "weight": "90 kg", "date": "2026-10-13T08:30:00" }"""))).Result!;
        Assert.Null(kept.WeightKg);
        Assert.Contains("already in Milo", kept.Message);
        Assert.Equal(82.4m, Assert.Single(weights.Entries).Value);
    }

    [Fact]
    public async Task Unreadable_IsRefused_AndTheErrorShowsInSettings()
    {
        var (service, _, _, _) = Service();
        var key = (await service.CreateKeyAsync(1)).Result!.Key;

        var response = await service.IngestAsync(key, Json("""{ "type": "Run" }"""));

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, response.Status);
        Assert.Contains("duration", (await service.GetAsync(1)).Result!.LastError);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static (WatchService Service, FakeWatchRepository Watch, FakePlanRepository Plan, FakeWeightRepository Weights) Service()
    {
        var plan = new FakePlanRepository();
        var weights = new FakeWeightRepository();
        var settings = new FakeSettingRepository();
        var clock = new FakeClock(Today) { Now = Now, Zone = Lisbon };
        var watch = new FakeWatchRepository();
        var days = new DayService(new FakeDayRepository(plan), plan, weights, clock, TestSupport.Globalization);
        return (new WatchService(watch, days, new WeightService(weights, settings, clock, TestSupport.Globalization), clock, TestSupport.Globalization), watch, plan, weights);
    }
}
