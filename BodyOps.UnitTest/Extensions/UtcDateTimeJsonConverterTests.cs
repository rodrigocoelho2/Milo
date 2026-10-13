using System.Text.Json;
using BodyOps.WebApi.Extensions.Json;

namespace BodyOps.UnitTest.Extensions;

public sealed class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        JsonSetup.Apply(options);
        return options;
    }

    [Fact]
    public void DatabaseTimestamps_AreWrittenAsUtc()
    {
        var unspecified = new DateTime(2026, 10, 8, 12, 30, 15, 123, DateTimeKind.Unspecified);

        Assert.Equal("\"2026-10-08T12:30:15.123Z\"", JsonSerializer.Serialize(unspecified, Options));
        Assert.Equal("{\"at\":null}", JsonSerializer.Serialize(new { At = (DateTime?)null }, Options));
    }

    [Fact]
    public void EnumsAreCamelCaseStrings() =>
        Assert.Equal("\"done\"", JsonSerializer.Serialize(BodyOps.Library.Enums.TrainingState.Done, Options));
}
