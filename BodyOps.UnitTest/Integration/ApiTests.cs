using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BodyOps.UnitTest.Integration;

/// <summary>The page's API end to end, against SQL Server. Each test gets its own app and database.</summary>
public sealed class ApiTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")).DateTime);

    private static int Weekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {body}");
        return string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task Register(HttpClient client, string username, string password = "squat-2026") =>
        await Json(await client.PostAsJsonAsync("/api/auth/register", new { username, password }), HttpStatusCode.Created);

    private static async Task<JsonElement> SaveExercises(HttpClient client, int weekday, params object[] exercises) =>
        await Json(await client.PutAsJsonAsync($"/api/plan/{weekday}/exercises", exercises));

    private static async Task<JsonElement> SaveMeals(HttpClient client, int weekday, params object[] meals) =>
        await Json(await client.PutAsJsonAsync($"/api/plan/{weekday}/meals", meals));

    private static JsonElement DayOf(JsonElement week, DateOnly date) =>
        week.GetProperty("days").EnumerateArray().Single(day => day.GetProperty("date").GetString() == Iso(date));

    [SqlFact]
    public async Task Health_ReportsFirstRunUntilTheFirstAccount()
    {
        using var app = new TestApp(allowRegistration: false);
        var client = app.NewClient();

        var before = await Json(await client.GetAsync("/api/health"));
        Assert.True(before.GetProperty("firstRun").GetBoolean());
        Assert.True(before.GetProperty("registration").GetBoolean());

        await Register(client, "Rodas");

        var after = await Json(await client.GetAsync("/api/health"));
        Assert.False(after.GetProperty("firstRun").GetBoolean());
        Assert.False(after.GetProperty("registration").GetBoolean());
    }

    [SqlFact]
    public async Task Api_RequiresASignedInUser_AndTheAppHeaderOnWrites()
    {
        using var app = new TestApp();
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.NewClient().GetAsync("/api/plan")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.NewClient().GetAsync("/api/days/week")).StatusCode);

        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/register", new { username = "Rodas", password = "squat-2026" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Missing the X-BodyOps header.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [SqlFact]
    public async Task EachAccount_HasItsOwnPlan()
    {
        using var app = new TestApp();
        var rodas = app.NewClient();
        var ana = app.NewClient();
        await Register(rodas, "Rodas");
        await Register(ana, "Ana");

        await SaveExercises(rodas, 2, new { name = "Pec deck", detail = "3x12" });

        var anaPlan = await Json(await ana.GetAsync("/api/plan"));
        Assert.All(anaPlan.GetProperty("days").EnumerateArray(), day => Assert.Equal(0, day.GetProperty("exercises").GetArrayLength()));
        var rodasPlan = await Json(await rodas.GetAsync("/api/plan"));
        Assert.Equal("Pec deck", rodasPlan.GetProperty("days")[1].GetProperty("exercises")[0].GetProperty("name").GetString());
    }

    [SqlFact]
    public async Task Plan_SaveKeepsIdsOfItemsSentBack_AndReplacesFoods()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");

        var first = await SaveMeals(client, 3, new { name = "Breakfast", foods = new[] { new { name = "Whey", amount = 25, unit = "grams" } } });
        var id = first.GetProperty("meals")[0].GetProperty("id").GetInt64();

        var second = await SaveMeals(client, 3,
            new { id, name = "Breakfast", foods = new[] { new { name = "Banana", amount = 1, unit = "pieces" } } },
            new { id = (long?)null, name = "Lunch", foods = Array.Empty<object>() });

        Assert.Equal(id, second.GetProperty("meals")[0].GetProperty("id").GetInt64());
        Assert.Equal("pieces", second.GetProperty("meals")[0].GetProperty("foods")[0].GetProperty("unit").GetString());
        Assert.Equal(2, second.GetProperty("meals").GetArrayLength());

        var copied = await Json(await client.PostAsJsonAsync("/api/plan/3/copy", new { part = "meals", targets = new[] { 4, 5 } }));
        Assert.Equal(2, copied.GetProperty("days")[4].GetProperty("meals").GetArrayLength());
    }

    [SqlFact]
    public async Task LoggingADay_CopiesThePlan_AndLaterPlanEditsOnlyChangeThisWeek()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");

        var weekday = Weekday(Today);
        var lastWeek = Today.AddDays(-7);
        var plan = await SaveExercises(client, weekday, new { name = "Incline chest press", detail = "3x12" });
        var exerciseId = plan.GetProperty("exercises")[0].GetProperty("id").GetInt64();
        await SaveMeals(client, weekday, new { name = "Breakfast", foods = new[] { new { name = "Whey", amount = 25, unit = "grams" } } });
        var mealId = (await Json(await client.GetAsync("/api/plan"))).GetProperty("days")[weekday - 1].GetProperty("meals")[0].GetProperty("id").GetInt64();

        // Last week: 100 kg, 3×10. Today: 102.5 kg.
        await Json(await client.PutAsJsonAsync($"/api/days/{Iso(lastWeek)}/exercises/{exerciseId}", new { loadKg = 100, sets = new int?[] { 10, 10, 10 } }));
        var week = await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today)}/exercises/{exerciseId}", new { loadKg = 102.5, sets = new int?[] { 12, 11, null } }));
        await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today)}/meals/{mealId}", new { eaten = true }));

        var exercise = DayOf(week, Today).GetProperty("exercises")[0];
        Assert.Equal("done", DayOf(week, Today).GetProperty("trainingState").GetString());
        Assert.Equal(102.5m, exercise.GetProperty("loadKg").GetDecimal());
        Assert.Equal(3, exercise.GetProperty("sets").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, exercise.GetProperty("sets")[2].ValueKind);
        Assert.Equal(Iso(lastWeek), exercise.GetProperty("last").GetProperty("date").GetString());
        Assert.True(exercise.GetProperty("isPr").GetBoolean());
        Assert.Equal(102.5m * 23, exercise.GetProperty("volumeKg").GetDecimal());

        // Rename the exercise in the plan: this week follows (keeping the load), last week keeps its name.
        await SaveExercises(client, weekday, new { id = exerciseId, name = "Incline machine press", detail = "3x12" });

        var thisWeek = await Json(await client.GetAsync($"/api/days/week?date={Iso(Today)}"));
        var renamed = DayOf(thisWeek, Today).GetProperty("exercises")[0];
        Assert.Equal("Incline machine press", renamed.GetProperty("name").GetString());
        Assert.Equal(102.5m, renamed.GetProperty("loadKg").GetDecimal());
        Assert.True(DayOf(thisWeek, Today).GetProperty("meals")[0].GetProperty("eaten").GetBoolean());

        var previous = await Json(await client.GetAsync($"/api/days/week?date={Iso(lastWeek)}"));
        Assert.Equal("Incline chest press", DayOf(previous, lastWeek).GetProperty("exercises")[0].GetProperty("name").GetString());

        var history = await Json(await client.GetAsync("/api/exercises/history?name=incline%20chest%20press"));
        Assert.Equal(1, history.GetProperty("sessions").GetArrayLength());
        Assert.Equal(100m, history.GetProperty("bestKg").GetDecimal());
    }

    [SqlFact]
    public async Task DayNamesMealTimesScoreStreakAndRecords()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");

        var weekday = Weekday(Today);
        var named = await Json(await client.PutAsJsonAsync($"/api/plan/{weekday}/name", new { name = "Upper body" }));
        Assert.Equal("Upper body", named.GetProperty("name").GetString());

        var plan = await SaveExercises(client, weekday, new { name = "Pec deck", detail = "3x12" });
        var exerciseId = plan.GetProperty("exercises")[0].GetProperty("id").GetInt64();
        var meals = await SaveMeals(client, weekday, new { name = "Breakfast", time = "8:00", foods = Array.Empty<object>() });
        Assert.Equal("08:00", meals.GetProperty("meals")[0].GetProperty("time").GetString());
        var mealId = meals.GetProperty("meals")[0].GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/plan/{weekday}/meals", new[] { new { name = "Lunch", time = "noon" } })).StatusCode);

        // Copying exercises carries the name along.
        var target = weekday == 7 ? 1 : weekday + 1;
        var copied = await Json(await client.PostAsJsonAsync($"/api/plan/{weekday}/copy", new { part = "exercises", targets = new[] { target } }));
        Assert.Equal("Upper body", copied.GetProperty("days")[target - 1].GetProperty("name").GetString());

        // A week ago: 65 kg; today 70 kg, all sets, breakfast and a weigh-in.
        await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today.AddDays(-7))}/exercises/{exerciseId}", new { loadKg = 65, sets = new int?[] { 12, 12, 12 } }));
        await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today)}/meals/{mealId}", new { eaten = true }));
        await Json(await client.PutAsJsonAsync($"/api/weights/{Iso(Today)}", new { weightKg = 97.6 }));
        var week = await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today)}/exercises/{exerciseId}", new { loadKg = 70, sets = new int?[] { 12, 12, 12 } }));

        var day = DayOf(week, Today);
        Assert.Equal("Upper body", day.GetProperty("name").GetString());
        Assert.Equal("08:00", day.GetProperty("meals")[0].GetProperty("time").GetString());
        Assert.Equal(97.6m, day.GetProperty("weightKg").GetDecimal());
        Assert.Equal(3, day.GetProperty("setsDone").GetInt32());
        Assert.Equal(100, day.GetProperty("score").GetInt32());

        var streak = await Json(await client.GetAsync("/api/days/streak"));
        Assert.Equal(100, streak.GetProperty("todayScore").GetInt32());
        Assert.Equal(1, streak.GetProperty("current").GetInt32());

        var records = await Json(await client.GetAsync("/api/exercises/records"));
        Assert.Equal(70m, records[0].GetProperty("loadKg").GetDecimal());
        Assert.Equal(65m, records[0].GetProperty("previousKg").GetDecimal());

        var exercises = await Json(await client.GetAsync("/api/exercises"));
        Assert.Equal(2, exercises[0].GetProperty("trend").GetArrayLength());

        // Rest between sets: 90 by default, kept when only the goal is saved.
        Assert.Equal(90, (await Json(await client.GetAsync("/api/settings"))).GetProperty("restSeconds").GetInt32());
        await Json(await client.PutAsJsonAsync("/api/settings", new { weightGoalKg = 90, restSeconds = 120 }));
        var settings = await Json(await client.PutAsJsonAsync("/api/settings", new { weightGoalKg = 88 }));
        Assert.Equal(120, settings.GetProperty("restSeconds").GetInt32());

        // The backup carries the name, the times and the rest.
        var backup = await Json(await client.GetAsync("/api/backup"));
        Assert.Equal("Upper body", backup.GetProperty("plan")[weekday - 1].GetProperty("name").GetString());
        Assert.Equal("08:00", backup.GetProperty("plan")[weekday - 1].GetProperty("meals")[0].GetProperty("time").GetString());
        Assert.Equal(120, backup.GetProperty("settings").GetProperty("restSeconds").GetInt32());
    }

    [SqlFact]
    public async Task ResetWeek_CanBeUndone()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");
        await Json(await client.PutAsJsonAsync($"/api/days/{Iso(Today)}/training", new { state = "missed" }));

        var removed = await Json(await client.DeleteAsync($"/api/days/week/{Iso(Today)}"));
        Assert.Equal(1, removed.GetArrayLength());
        Assert.False(DayOf(await Json(await client.GetAsync("/api/days/week")), Today).GetProperty("logged").GetBoolean());

        var restored = await Json(await client.PostAsJsonAsync($"/api/days/week/{Iso(Today)}/restore", removed));
        Assert.Equal("missed", DayOf(restored, Today).GetProperty("trainingState").GetString());

        var consistency = await Json(await client.GetAsync("/api/days/consistency?weeks=1"));
        Assert.Equal(0, consistency.EnumerateArray().Single(day => day.GetProperty("date").GetString() == Iso(Today)).GetProperty("score").GetDouble());
    }

    [SqlFact]
    public async Task Weights_AndGoal()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");

        await Json(await client.PutAsJsonAsync("/api/settings", new { weightGoalKg = 90 }));
        await Json(await client.PutAsJsonAsync($"/api/weights/{Iso(Today.AddDays(-1))}", new { weightKg = 98.8 }));
        var weights = await Json(await client.PutAsJsonAsync($"/api/weights/{Iso(Today)}", new { weightKg = 98.0 }));

        Assert.Equal(2, weights.GetProperty("entries").GetArrayLength());
        Assert.Equal(90m, weights.GetProperty("goalKg").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/weights/{Iso(Today.AddDays(1))}", new { weightKg = 98.0 })).StatusCode);

        var left = await Json(await client.DeleteAsync($"/api/weights/{Iso(Today)}"));
        Assert.Equal(1, left.GetProperty("entries").GetArrayLength());
    }

    [SqlFact]
    public async Task Backup_ImportsACorpoOpsFile_AndExportsItBack()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");
        var file = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Backups", "corpoops-sample.json"));

        var result = await Json(await client.PostAsync("/api/backup", new StringContent(file, System.Text.Encoding.UTF8, "application/json")));
        Assert.Equal("corpoops", result.GetProperty("source").GetString());
        Assert.Equal(2, result.GetProperty("days").GetInt32());

        var july = await Json(await client.GetAsync("/api/days/week?date=2026-07-21"));
        var tuesday = DayOf(july, new DateOnly(2026, 7, 21));
        Assert.Equal("Aveia 50 g", tuesday.GetProperty("meals")[0].GetProperty("foods").GetString());
        Assert.Equal(100m, tuesday.GetProperty("exercises")[0].GetProperty("loadKg").GetDecimal());
        Assert.Equal("Face Pull", tuesday.GetProperty("exercises")[2].GetProperty("name").GetString());

        var exported = await client.GetAsync("/api/backup");
        Assert.Contains("attachment", exported.Content.Headers.ContentDisposition?.ToString() ?? exported.Headers.ToString());
        var backup = await Json(exported);
        Assert.Equal("bodyops-backup", backup.GetProperty("format").GetString());
        Assert.Equal(2, backup.GetProperty("days").GetArrayLength());

        // Importing our own export gives the same days back.
        var again = await Json(await client.PostAsJsonAsync("/api/backup", backup));
        Assert.Equal("bodyops", again.GetProperty("source").GetString());
        Assert.Equal(2, again.GetProperty("days").GetInt32());
    }

    [SqlFact]
    public async Task DeletingAnAccount_RemovesItsData()
    {
        using var app = new TestApp();
        var client = app.NewClient();
        await Register(client, "Rodas");
        await SaveExercises(client, 1, new { name = "Hack squat", detail = "3x12" });
        await Json(await client.PutAsJsonAsync($"/api/weights/{Iso(Today)}", new { weightKg = 98.0 }));

        await Json(await client.PostAsJsonAsync("/api/auth/delete-account", new { password = "squat-2026" }), HttpStatusCode.NoContent);

        Assert.Equal(0, (int)app.Database.Scalar("SELECT COUNT(*) FROM dbo.PlanExercise")!);
        Assert.Equal(0, (int)app.Database.Scalar("SELECT COUNT(*) FROM dbo.WeightEntry")!);
    }
}
