using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Plans;

public sealed class CardioKindsTests
{
    [Theory]
    [InlineData("Passadeira", CardioKind.Treadmill)]
    [InlineData("Cardio na passadeira", CardioKind.Treadmill)]
    [InlineData("Treadmill walk", CardioKind.Treadmill)]
    [InlineData("Bicicleta", CardioKind.Bike)]
    [InlineData("Cycling", CardioKind.Bike)]
    [InlineData("Elíptica", CardioKind.Elliptical)]
    [InlineData("Remo", CardioKind.Rowing)]
    [InlineData("Corrida", CardioKind.Run)]
    [InlineData("Natação", CardioKind.Swim)]
    [InlineData("HIIT", CardioKind.Other)]
    public void Detect_WorksOutTheKindFromTheName(string name, CardioKind expected) =>
        Assert.Equal(expected, CardioKinds.Detect(name));

    [Fact]
    public void Resolve_PrefersTheKindPickedByHand() =>
        Assert.Equal(CardioKind.Rowing, CardioKinds.Resolve("Bicicleta", CardioKind.Rowing));

    [Theory]
    [InlineData(CardioKind.Treadmill, false)]
    [InlineData(CardioKind.Elliptical, false)]
    [InlineData(CardioKind.Stairs, false)]
    [InlineData(CardioKind.Bike, true)]
    [InlineData(CardioKind.Run, true)]
    [InlineData(CardioKind.Other, true)]
    public void UsesDistance_IsTimeOnlyForTreadmillEllipticalAndStairs(CardioKind kind, bool expected) =>
        Assert.Equal(expected, CardioKinds.UsesDistance(kind));

    [Fact]
    public async Task SavingTheKind_KeepsItForCardio_AndDropsItForStrength()
    {
        var repository = new FakePlanRepository();
        var service = new PlanService(repository, new FakeClock(TestSupport.Tuesday), TestSupport.Globalization);

        var response = await service.SaveExercisesAsync(1, 2,
        [
            new PlanExerciseSaveDTO { Type = ExerciseType.Cardio, Name = "Cardio", Detail = "30 min", CardioKind = CardioKind.Treadmill },
            new PlanExerciseSaveDTO { Type = ExerciseType.Strength, Name = "Pec deck", Detail = "3x12", CardioKind = CardioKind.Bike },
            new PlanExerciseSaveDTO { Type = ExerciseType.Cardio, Name = "Bicicleta", Detail = "20 min" },
        ]);

        var exercises = response.Result!.Exercises;
        Assert.Equal(CardioKind.Treadmill, exercises[0].CardioKind);
        Assert.False(exercises[0].CardioKindAuto);
        Assert.False(exercises[0].CardioDistance);
        Assert.Null(exercises[1].CardioKind);
        Assert.Null(repository.Exercises.Single(exercise => exercise.Name == "Pec deck").CardioKind);
        Assert.Equal(CardioKind.Bike, exercises[2].CardioKind);
        Assert.True(exercises[2].CardioKindAuto);
        Assert.True(exercises[2].CardioDistance);
    }
}
