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

    [Fact]
    public void Targets_NeverMoreThanTheSetLimit() =>
        Assert.Equal(ExerciseDetailParser.MaxSets, ExerciseDetailParser.Targets("30x5").Count);
}
