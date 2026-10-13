using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Backups.Services;

namespace BodyOps.UnitTest.Backups;

public sealed class LegacyBackupConverterTests
{
    private static JsonElement Sample()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Backups", "corpoops-sample.json")));
        return document.RootElement.Clone();
    }

    [Fact]
    public void IsLegacy_NeedsDietAndWorkout()
    {
        Assert.True(LegacyBackupConverter.IsLegacy(Sample()));
        Assert.False(LegacyBackupConverter.IsLegacy(JsonDocument.Parse("""{"format":"bodyops-backup"}""").RootElement));
    }

    [Fact]
    public void Convert_ReadsThePlan_WithUnitsMusclesAndCleanNames()
    {
        var backup = LegacyBackupConverter.Convert(Sample());

        var tuesday = backup.Plan.Single(day => day.Weekday == 2);
        Assert.Equal(["Pequeno-Almoço", "Almoço"], tuesday.Meals.Select(meal => meal.Name));
        Assert.Equal(FoodUnit.Pieces, tuesday.Meals[0].Foods[1].Unit);
        Assert.Equal("Chest Press Inclinada", tuesday.Exercises[0].Name);
        Assert.Null(tuesday.Exercises[0].Muscles);
        Assert.Equal([Muscle.Biceps, Muscle.Forearms], tuesday.Exercises[1].Muscles!);
        Assert.Equal(ExerciseType.Cardio, backup.Plan.Single(day => day.Weekday == 3).Exercises[0].Type);
    }

    [Fact]
    public void Convert_UsesTheWeeksSnapshot_AndTheLoggedValues()
    {
        var backup = LegacyBackupConverter.Convert(Sample());

        var tuesday = backup.Days.Single(day => day.Date == new DateOnly(2026, 7, 21));
        Assert.Equal(TrainingState.Done, tuesday.TrainingState);
        var breakfast = Assert.Single(tuesday.Meals);
        Assert.Equal("Aveia 50 g", breakfast.Foods);
        Assert.True(breakfast.Eaten);

        var press = tuesday.Exercises[0];
        Assert.Equal(100, press.LoadKg);
        Assert.Equal(new int?[] { 10, 10, 10 }, press.Sets);
        Assert.Equal(new int?[] { 10, 10 }, tuesday.Exercises[1].Sets);

        var orphan = tuesday.Exercises.Single(exercise => exercise.Name == "Face Pull");
        Assert.Equal(new int?[] { 12, 10 }, orphan.Sets);

        // The plan and the logged day share ids, so the import can link them.
        Assert.Equal(backup.Plan.Single(day => day.Weekday == 2).Exercises[0].Id, press.PlanExerciseId);
    }

    [Fact]
    public void Convert_ReadsCardio_AndSkipsDaysWithNothingLogged()
    {
        var backup = LegacyBackupConverter.Convert(Sample());

        var wednesday = backup.Days.Single(day => day.Date == new DateOnly(2026, 7, 22));
        Assert.Equal(32, wednesday.Exercises[0].CardioMinutes);
        Assert.Equal(5.2m, wednesday.Exercises[0].CardioKm);
        Assert.DoesNotContain(backup.Days, day => day.Date == new DateOnly(2026, 7, 23));
    }

    [Fact]
    public void Convert_ReadsWeighInsAndGoal()
    {
        var backup = LegacyBackupConverter.Convert(Sample());

        Assert.Equal([100.4m, 98.8m], backup.Weights.Select(entry => entry.WeightKg));
        Assert.Equal(90, backup.Settings.WeightGoalKg);
    }

    [Theory]
    [InlineData("3x10", new[] { 10, 10, 10 })]
    [InlineData("3×12, 3×10", new[] { 12, 12, 12, 10, 10, 10 })]
    [InlineData("12, 11, 10", new[] { 12, 11, 10 })]
    [InlineData("", new int[0])]
    public void ParseSets_ReadsTheRealField(string real, int[] expected) =>
        Assert.Equal(expected.Select(reps => (int?)reps), LegacyBackupConverter.ParseSets(real));
}
