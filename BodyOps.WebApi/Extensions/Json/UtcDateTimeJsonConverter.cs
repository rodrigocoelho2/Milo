using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BodyOps.WebApi.Extensions.Json;

/// <summary>
/// Every timestamp in the database is UTC, but SQL Server's DATETIME2 comes back without a kind.
/// This writes them as ISO 8601 with a trailing "Z", so the page's <c>new Date(…)</c> reads them as UTC.
/// </summary>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }
}
