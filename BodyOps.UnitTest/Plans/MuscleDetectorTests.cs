using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Plans;

public sealed class MuscleDetectorTests
{
    [Theory]
    [InlineData("Hack squat", Muscle.Quads)]
    [InlineData("Leg press 45°", Muscle.Quads)]
    [InlineData("Bulgarian split squat", Muscle.Quads)]
    [InlineData("Leg extension", Muscle.Quads)]
    [InlineData("Leg curl", Muscle.Hamstrings)]
    [InlineData("Hip abductor machine", Muscle.Glutes)]
    [InlineData("Incline chest press", Muscle.Chest)]
    [InlineData("Wide-grip lat pulldown", Muscle.Lats)]
    [InlineData("Machine shoulder press", Muscle.Shoulders)]
    [InlineData("Pec deck", Muscle.Chest)]
    [InlineData("Machine biceps curl", Muscle.Biceps)]
    [InlineData("Machine triceps pushdown", Muscle.Triceps)]
    [InlineData("Incline dumbbell press", Muscle.Chest)]
    [InlineData("Machine lateral raises", Muscle.Shoulders)]
    [InlineData("Low-pulley French press", Muscle.Triceps)]
    [InlineData("Wide-grip chest-supported T-bar row", Muscle.Lats)]
    [InlineData("Rear delt machine", Muscle.Shoulders)]
    [InlineData("Machine preacher curl", Muscle.Biceps)]
    [InlineData("Supino Inclinado com Halteres", Muscle.Chest)]
    [InlineData("Maquina Elevações Laterais", Muscle.Shoulders)]
    [InlineData("Cadeira Flexora", Muscle.Hamstrings)]
    [InlineData("Puxador à Frente Pega Aberta", Muscle.Lats)]
    public void Detect_FindsTheMainMover(string name, Muscle main)
    {
        var muscles = MuscleDetector.Detect(name, ExerciseType.Strength, null);

        Assert.Equal(1, muscles[main]);
        Assert.Equal(main, muscles.MaxBy(pair => pair.Value).Key);
    }

    [Fact]
    public void Detect_MatchesWholeWordsOnly_SoLateralIsNotLats() =>
        Assert.DoesNotContain(Muscle.Lats, MuscleDetector.Detect("Lateral raise", ExerciseType.Strength, null).Keys);

    [Fact]
    public void Detect_CardioHasNoMuscles_UnlessPickedByHand()
    {
        Assert.Empty(MuscleDetector.Detect("Cycling", ExerciseType.Cardio, null));
        Assert.Equal([Muscle.Quads, Muscle.Calves], MuscleDetector.Detect("Cycling", ExerciseType.Cardio, "11,13").Keys);
    }

    [Fact]
    public void Detect_HandPickedMusclesWin_AndAnEmptyPickMeansNone()
    {
        Assert.Equal([Muscle.Chest], MuscleDetector.Detect("Leg press", ExerciseType.Strength, "1").Keys);
        Assert.Empty(MuscleDetector.Detect("Leg press", ExerciseType.Strength, ""));
    }

    [Fact]
    public void FormatAndParse_RoundTrip_IgnoringUnknownAndRepeated()
    {
        Assert.Equal("1,4", MuscleDetector.Format([Muscle.Chest, Muscle.Triceps, Muscle.Chest]));
        Assert.Null(MuscleDetector.Format(null));
        Assert.Equal([Muscle.Chest, Muscle.Triceps], MuscleDetector.Parse("1, 4,99,x,1"));
    }
}
