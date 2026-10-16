using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.UnitTest.Plans;

public sealed class ExerciseDetailParserTests
{
    [Theory]
    [InlineData("3x12", new[] { 12, 12, 12 })]
    [InlineData("3 × 12", new[] { 12, 12, 12 })]
    [InlineData("3X10 @ 60kg", new[] { 10, 10, 10 })]
    [InlineData("2x12, 2x8", new[] { 12, 12, 8, 8 })]
    [InlineData("4 sets", new[] { 0, 0, 0, 0 })]
    [InlineData("4 séries", new[] { 0, 0, 0, 0 })]
    [InlineData("20 km", new int[0])]
    [InlineData("", new int[0])]
    public void Targets_ReadsSetsAndReps(string detail, int[] expected) =>
        Assert.Equal(expected, ExerciseDetailParser.Targets(detail));

    [Theory]
    [InlineData("3x8-12", new[] { 12, 12, 12 }, new[] { 8, 8, 8 }, true)]
    [InlineData("3 × 8–12", new[] { 12, 12, 12 }, new[] { 8, 8, 8 }, true)]
    [InlineData("2x12-10", new[] { 12, 12 }, new[] { 10, 10 }, true)]
    [InlineData("3x10-60kg", new[] { 10, 10, 10 }, new[] { 10, 10, 10 }, false)]
    [InlineData("3x10 - 60 kg", new[] { 10, 10, 10 }, new[] { 10, 10, 10 }, false)]
    [InlineData("3x10", new[] { 10, 10, 10 }, new[] { 10, 10, 10 }, false)]
    [InlineData("2x10-12, 1x6", new[] { 12, 12, 6 }, new[] { 10, 10, 6 }, true)]
    public void Ranges_TargetIsTheTop_MinsAreTheBottom(string detail, int[] targets, int[] mins, bool isRange)
    {
        Assert.Equal(targets, ExerciseDetailParser.Targets(detail));
        Assert.Equal(mins, ExerciseDetailParser.Mins(detail));
        Assert.Equal(isRange, ExerciseDetailParser.IsRange(detail));
    }

    [Fact]
    public void Targets_NeverMoreThanTheSetLimit() =>
        Assert.Equal(ExerciseDetailParser.MaxSets, ExerciseDetailParser.Targets("30x5").Count);
}
