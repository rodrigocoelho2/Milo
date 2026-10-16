using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Watch.Services;

/// <summary>A workout read from what the Shortcut sent, times in the app's time zone.</summary>
/// <param name="Kind">The Watch's workout type as sent ("Traditional Strength Training", "Indoor Walk").</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended.</param>
/// <param name="DurationSeconds">How long it lasted.</param>
/// <param name="ActiveKcal">Active calories, or null.</param>
/// <param name="AvgBpm">Average heart rate, or null.</param>
/// <param name="MaxBpm">Maximum heart rate, or null.</param>
/// <param name="DistanceKm">Distance, or null.</param>
/// <param name="HeartRates">The heart-rate curve, at most <see cref="WatchPayload.MaxCurvePoints"/> points.</param>
internal sealed record WatchWorkoutData(string Kind, DateTime StartedAt, DateTime EndedAt, int DurationSeconds, decimal? ActiveKcal, int? AvgBpm, int? MaxBpm, decimal? DistanceKm, IReadOnlyList<int> HeartRates);

/// <summary>A weigh-in read from what the Shortcut sent.</summary>
/// <param name="WeightKg">The weight.</param>
/// <param name="Date">The day it was measured.</param>
internal sealed record WatchWeightData(decimal WeightKg, DateOnly Date);

/// <summary>What the Shortcut sent, read.</summary>
/// <param name="Workout">The workout, or null.</param>
/// <param name="Weight">The weigh-in, or null.</param>
/// <param name="Error">What was wrong, or null.</param>
internal sealed record WatchPayloadData(WatchWorkoutData? Workout, WatchWeightData? Weight, string? Error);

/// <summary>What a Watch workout is, for Milo.</summary>
internal enum WatchCategory
{
    /// <summary>Strength, HIIT, core…: goes with the day's session.</summary>
    Strength,

    /// <summary>Walk, run, bike…: can fill in planned cardio.</summary>
    Cardio,

    /// <summary>Anything else (yoga, tennis…): shown, matched to nothing.</summary>
    Other,
}

/// <summary>
/// Reads what the iOS Shortcut sends. Shortcuts is loose with types: numbers come with units ("412 kcal", "82,4 kg"),
/// durations as "58 min" or "0:58:12", dates in the phone's language ("16 Oct 2026 at 19:12") and lists as text with
/// one value per line. So every field is read leniently, under several names, flat or inside "workout" / "weight".
/// Pure, unit-tested.
/// </summary>
internal static partial class WatchPayload
{
    /// <summary>The most heart-rate points kept for the curve.</summary>
    public const int MaxCurvePoints = 60;

    /// <summary>The longest workout accepted.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

    private static readonly string[] TypeNames = ["type", "workouttype", "activity", "activitytype", "name", "kind"];
    private static readonly string[] StartNames = ["start", "startdate", "startedat", "starttime", "begin"];
    private static readonly string[] EndNames = ["end", "enddate", "endedat", "endtime", "finish"];
    private static readonly string[] KcalNames = ["activekcal", "activeenergy", "activeenergyburned", "activecalories", "kcal", "calories", "energy", "totalenergyburned"];
    private static readonly string[] AvgNames = ["avgbpm", "averageheartrate", "avgheartrate", "heartrateavg", "avghr", "averagebpm"];
    private static readonly string[] MaxNames = ["maxbpm", "maxheartrate", "maximumheartrate", "heartratemax", "maxhr"];
    private static readonly string[] DistanceNames = ["distancekm", "distance", "km", "totaldistance"];
    private static readonly string[] CurveNames = ["heartrates", "heartrate", "hr", "bpm", "heartratesamples", "samples"];
    private static readonly string[] WeightNames = ["weightkg", "weight", "bodymass", "kg"];
    private static readonly string[] WeightDateNames = ["weightdate", "date", "start", "startdate", "measuredat"];

    private static readonly CultureInfo[] Cultures =
        [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-GB"), CultureInfo.GetCultureInfo("pt-PT"), CultureInfo.GetCultureInfo("pt-BR")];

    /// <summary>
    /// Reads a payload.
    /// </summary>
    /// <param name="root">The JSON body.</param>
    /// <param name="zone">The app's time zone (times without an offset are taken as local).</param>
    /// <param name="now">The wall clock in that zone (a workout without times ends now).</param>
    /// <returns>The workout and/or weigh-in, or an error.</returns>
    public static WatchPayloadData Parse(JsonElement root, TimeZoneInfo zone, DateTime now)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new WatchPayloadData(null, null, "Send a JSON dictionary (Get Contents of URL › Request Body › JSON).");
        }

        var fields = Fields(root);
        var workoutFields = Child(fields, "workout") ?? fields;
        var weightFields = Child(fields, "weight") ?? Child(fields, "bodymass") ?? fields;

        WatchWorkoutData? workout = null;
        string? error = null;
        var nested = workoutFields != fields;
        if (nested
            ? workoutFields.Count > 0
            : HasAny(fields, TypeNames, EndNames, ["duration", "durationseconds", "seconds", "durationminutes", "minutes"], KcalNames))
        {
            (workout, error) = Workout(workoutFields, zone, now);
        }

        WatchWeightData? weight = null;
        var weightValue = weightFields == fields ? First(fields, WeightNames) : First(weightFields, ["value", "kg", "weightkg", "weight", "quantity"]);
        if (weightValue is { } value)
        {
            var dateField = weightFields != fields ? First(weightFields, WeightDateNames)
                : First(fields, ["weightdate"]) ?? (workout is null ? First(fields, WeightDateNames) : null);
            var date = dateField is { } at && Date(at, zone, now) is { } when
                ? DateOnly.FromDateTime(when)
                : DateOnly.FromDateTime(now);
            if (Weight(value) is { } kg)
            {
                weight = new WatchWeightData(kg, date > DateOnly.FromDateTime(now) ? DateOnly.FromDateTime(now) : date);
            }
            else
            {
                error ??= "The weight isn't a number.";
            }
        }

        if (workout is null && weight is null)
        {
            error ??= "Nothing to import: send a workout (type, start, end, duration, active energy, heart rate) or a weight.";
        }

        return new WatchPayloadData(workout, weight, error);
    }

    /// <summary>
    /// What a Watch workout type is (English or Portuguese).
    /// </summary>
    /// <param name="kind">The type as sent.</param>
    /// <returns>The category.</returns>
    public static WatchCategory Category(string kind)
    {
        var k = kind.ToLowerInvariant();
        if (CardioPattern().IsMatch(k))
        {
            return WatchCategory.Cardio;
        }

        return StrengthPattern().IsMatch(k) ? WatchCategory.Strength : WatchCategory.Other;
    }

    /// <summary>
    /// The planned cardio kinds a Watch workout can fill in, best match first.
    /// </summary>
    /// <param name="kind">The type as sent.</param>
    /// <returns>The kinds, or empty.</returns>
    public static IReadOnlyList<CardioKind> CardioKinds(string kind)
    {
        var k = kind.ToLowerInvariant();
        var indoor = k.Contains("indoor", StringComparison.Ordinal) || k.Contains("interior", StringComparison.Ordinal) || k.Contains("passadeira", StringComparison.Ordinal) || k.Contains("treadmill", StringComparison.Ordinal);
        if (Regex.IsMatch(k, "walk|caminhad|hik|passadeira|treadmill"))
        {
            return indoor ? [CardioKind.Treadmill, CardioKind.Walk] : [CardioKind.Walk, CardioKind.Treadmill];
        }

        if (Regex.IsMatch(k, "run|corrid|jog"))
        {
            return indoor ? [CardioKind.Treadmill, CardioKind.Run] : [CardioKind.Run, CardioKind.Treadmill];
        }

        if (Regex.IsMatch(k, "cycl|bike|biking|ciclism|bicicleta|spin"))
        {
            return [CardioKind.Bike];
        }

        if (Regex.IsMatch(k, "ellip|el[ií]ptic"))
        {
            return [CardioKind.Elliptical];
        }

        if (Regex.IsMatch(k, "row|remo"))
        {
            return [CardioKind.Rowing];
        }

        if (Regex.IsMatch(k, "stair|step|escada"))
        {
            return [CardioKind.Stairs];
        }

        return Regex.IsMatch(k, "swim|nata[cç]") ? [CardioKind.Swim] : [];
    }

    /// <summary>
    /// A number from JSON or text: "412", "412 kcal", "82,4 kg", "1,234.5".
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or null.</returns>
    public static decimal? Number(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDecimal(out var number) ? number : null;
        }

        return value.ValueKind == JsonValueKind.String ? Number(value.GetString()) : null;
    }

    /// <summary>
    /// A number from text, with a comma or a dot as the decimal separator.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or null.</returns>
    public static decimal? Number(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberPattern().Match(text.Replace(' ', ' ').Replace(' ', ' '));
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);
        var lastComma = raw.LastIndexOf(',');
        var lastDot = raw.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both: the last one is the decimal separator.
            raw = lastComma > lastDot ? raw.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.') : raw.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (lastComma >= 0)
        {
            // "1,234" with three digits after one comma is a thousands separator; otherwise a decimal comma.
            raw = raw.Count(static c => c == ',') == 1 && raw.Length - lastComma - 1 != 3 ? raw.Replace(',', '.') : raw.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>
    /// A duration: seconds or minutes as a number (by its field name), "58 min", "1 h 2 min", "58m 12s", "0:58:12".
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="unit">"seconds", "minutes", or null to guess (a bare number over 300 is seconds).</param>
    /// <returns>Seconds, or null.</returns>
    public static int? Duration(JsonElement value, string? unit)
    {
        if (value.ValueKind == JsonValueKind.Number || (value.ValueKind == JsonValueKind.String && BareNumberPattern().IsMatch(value.GetString()!.Trim())))
        {
            if (Number(value) is not { } number || number <= 0)
            {
                return null;
            }

            var seconds = unit == "seconds" || (unit is null && number > 300) ? number : number * 60;
            return (int)Math.Round(seconds);
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim().ToLowerInvariant() : string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var clock = ClockPattern().Match(text);
        if (clock.Success)
        {
            var a = int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
            if (clock.Groups[3].Success)
            {
                return (a * 3600) + (b * 60) + int.Parse(clock.Groups[3].Value, CultureInfo.InvariantCulture);
            }

            // "58:12" is minutes and seconds; "1:05" hours and minutes.
            return a < 10 ? (a * 3600) + (b * 60) : (a * 60) + b;
        }

        double total = 0;
        var any = false;
        foreach (Match part in UnitPattern().Matches(text))
        {
            var amount = (double)(Number(part.Groups[1].Value) ?? 0);
            var u = part.Groups[2].Value;
            total += u.StartsWith('h') ? amount * 3600 : u.StartsWith('s') ? amount : amount * 60;
            any = true;
        }

        return any && total > 0 ? (int)Math.Round(total) : null;
    }

    /// <summary>
    /// A date and time: ISO 8601 (with or without offset), Unix seconds or milliseconds, or the phone's own format
    /// ("16 Oct 2026 at 19:12", "16/10/2026, 19:12", "Oct 16, 2026 at 7:12 PM"). When the day and month could be
    /// either way round, the reading nearest to <paramref name="near"/> (and not in the future) wins.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="zone">The app's time zone.</param>
    /// <param name="near">The wall clock now, in that zone.</param>
    /// <returns>The local time in that zone, or null.</returns>
    public static DateTime? Date(JsonElement value, TimeZoneInfo zone, DateTime near)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            var at = epoch > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : DateTimeOffset.FromUnixTimeSeconds(epoch);
            return TimeZoneInfo.ConvertTime(at, zone).DateTime;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = AtPattern().Replace(value.GetString()!.Trim().Replace('\u202f', ' ').Replace('\u00a0', ' '), " ");
        text = CommaBeforeTimePattern().Replace(text, " ");
        if (text.Length == 0)
        {
            return null;
        }

        var hasOffset = OffsetPattern().IsMatch(text);
        var readings = new List<DateTime>();
        foreach (var culture in Cultures)
        {
            if (hasOffset && DateTimeOffset.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var withOffset))
            {
                readings.Add(TimeZoneInfo.ConvertTime(withOffset, zone).DateTime);
            }
            else if (!hasOffset && DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var local) && local.Year > 2000)
            {
                readings.Add(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
            }
        }

        if (readings.Count == 0)
        {
            return null;
        }

        var limit = near.AddMinutes(10);
        var past = readings.Where(reading => reading <= limit).ToList();
        return (past.Count > 0 ? past : readings).OrderBy(reading => Math.Abs((near - reading).Ticks)).First();
    }

    /// <summary>
    /// Heart-rate values from a JSON list or text with one value per line (or comma-separated), kept between 25 and 250.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The values, in order.</returns>
    public static List<int> HeartRates(JsonElement value)
    {
        IEnumerable<decimal?> numbers = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(static item => item.ValueKind == JsonValueKind.Object ? First(Fields(item), ["value", "bpm", "heartrate", "quantity"]) is { } inner ? Number(inner) : null : Number(item)),
            JsonValueKind.String => SplitList(value.GetString()!).Select(static part => Number(part)),
            JsonValueKind.Number => [Number(value)],
            _ => [],
        };
        return [.. numbers.Where(static n => n is >= 25 and <= 250).Select(static n => (int)Math.Round(n!.Value))];
    }

    /// <summary>
    /// Shrinks a curve to at most <paramref name="points"/> values (the average of each slice).
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="points">The most points.</param>
    /// <returns>The curve.</returns>
    public static List<int> Curve(IReadOnlyList<int> values, int points = MaxCurvePoints)
    {
        if (values.Count <= points)
        {
            return [.. values];
        }

        var curve = new List<int>(points);
        for (var i = 0; i < points; i++)
        {
            var from = i * values.Count / points;
            var to = Math.Max(from + 1, (i + 1) * values.Count / points);
            curve.Add((int)Math.Round(values.Skip(from).Take(to - from).Average()));
        }

        return curve;
    }

    private static (WatchWorkoutData?, string?) Workout(Dictionary<string, JsonElement> fields, TimeZoneInfo zone, DateTime now)
    {
        var kind = First(fields, TypeNames) is { ValueKind: JsonValueKind.String } type ? Clean(type.GetString()!) : string.Empty;
        var start = First(fields, StartNames) is { } s ? Date(s, zone, now) : null;
        var end = First(fields, EndNames) is { } e ? Date(e, zone, now) : null;
        int? duration = null;
        foreach (var (name, unit) in new[] { ("durationseconds", "seconds"), ("seconds", "seconds"), ("durationminutes", "minutes"), ("minutes", "minutes"), ("duration", null as string) })
        {
            if (fields.TryGetValue(name, out var value) && Duration(value, unit) is { } seconds)
            {
                duration = seconds;
                break;
            }
        }

        if (start is { } from && end is { } to && to > from)
        {
            duration ??= (int)(to - from).TotalSeconds;
        }
        else if (duration is { } d)
        {
            if (start is { } begin)
            {
                end = begin.AddSeconds(d);
            }
            else
            {
                end ??= now;
                start = end.Value.AddSeconds(-d);
            }
        }

        if (duration is not { } length || length <= 0 || start is null || end is null)
        {
            return (null, "The workout has no duration, or no start and end.");
        }

        if (length > MaxDuration.TotalSeconds)
        {
            return (null, "That workout is longer than 12 hours.");
        }

        if (end > now.AddMinutes(10))
        {
            return (null, "That workout ends in the future. Check the phone's date and time.");
        }

        var curve = First(fields, CurveNames) is { } hr ? HeartRates(hr) : [];
        var avg = First(fields, AvgNames) is { } a ? Bpm(Number(a)) : null;
        var max = First(fields, MaxNames) is { } m ? Bpm(Number(m)) : null;
        if (curve.Count > 0)
        {
            avg ??= (int)Math.Round(curve.Average());
            max ??= curve.Max();
        }

        var kcal = First(fields, KcalNames) is { } k ? Energy(k) : null;
        var distance = First(fields, DistanceNames) is { } dist ? Distance(dist) : null;
        var startedAt = Minute(start.Value);
        return (new WatchWorkoutData(kind.Length > 0 ? kind : "Workout", startedAt, Minute(end.Value), length, kcal, avg, max, distance, Curve(curve)), null);
    }

    private static decimal? Energy(JsonElement value)
    {
        if (Number(value) is not { } number || number < 0)
        {
            return null;
        }

        var kj = value.ValueKind == JsonValueKind.String && value.GetString()!.Contains("kj", StringComparison.OrdinalIgnoreCase);
        var kcal = kj ? number / 4.184m : number;
        return kcal is > 0 and < 20000 ? Math.Round(kcal, 1) : null;
    }

    private static decimal? Distance(JsonElement value)
    {
        if (Number(value) is not { } number || number <= 0)
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.ToLowerInvariant() : string.Empty;
        var km = text.Contains("mi", StringComparison.Ordinal) ? number * 1.609344m
            : Regex.IsMatch(text, @"\d\s*m\b") ? number / 1000
            : number;
        return km is > 0 and < 1000 ? Math.Round(km, 2) : null;
    }

    private static decimal? Weight(JsonElement value)
    {
        if (Number(value) is not { } number || number <= 0)
        {
            return null;
        }

        var lb = value.ValueKind == JsonValueKind.String && Regex.IsMatch(value.GetString()!, "lb|pound", RegexOptions.IgnoreCase);
        return Math.Round(lb ? number * 0.45359237m : number, 1);
    }

    private static int? Bpm(decimal? value) => value is >= 25 and <= 250 ? (int)Math.Round(value.Value) : null;

    private static DateTime Minute(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);

    private static string Clean(string text)
    {
        var clean = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= 60 ? clean : clean[..60];
    }

    private static IEnumerable<string> SplitList(string text) =>
        text.Contains('\n', StringComparison.Ordinal) || text.Contains(';', StringComparison.Ordinal) || text.Contains('|', StringComparison.Ordinal)
            ? text.Split(['\n', '\r', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The object's fields, by name in lower case without spaces, dashes or underscores.</summary>
    private static Dictionary<string, JsonElement> Fields(JsonElement element)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var key = new string([.. property.Name.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
            fields.TryAdd(key, property.Value);
        }

        return fields;
    }

    private static Dictionary<string, JsonElement>? Child(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var child) && child.ValueKind == JsonValueKind.Object ? Fields(child) : null;

    private static JsonElement? First(Dictionary<string, JsonElement> fields, string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                && !(value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
            {
                return value;
            }
        }

        return null;
    }

    private static bool HasAny(Dictionary<string, JsonElement> fields, params string[][] groups) => groups.Any(names => First(fields, names) is not null);

    [GeneratedRegex(@"-?\d[\d\s.,]*")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^-?\d+([.,]\d+)?$")]
    private static partial Regex BareNumberPattern();

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$")]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(h(?:ours?|rs?)?|m(?:in(?:utes?|s)?)?|s(?:ec(?:onds?|s)?)?)\b")]
    private static partial Regex UnitPattern();

    [GeneratedRegex(@"\s+(?:at|às|as|a las|um)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex AtPattern();

    [GeneratedRegex(@",\s*(?=\d{1,2}[:h.]\d{2})")]
    private static partial Regex CommaBeforeTimePattern();

    [GeneratedRegex(@"(?:Z|[+-]\d{2}:?\d{2})$")]
    private static partial Regex OffsetPattern();

    [GeneratedRegex(@"walk|caminhad|hik|run|corrid|jog|cycl|bike|biking|ciclism|bicicleta|spin|ellip|el[ií]ptic|rowing|\brow|remo|stair|escada|swim|nata[cç]|treadmill|passadeira|cardio")]
    private static partial Regex CardioPattern();

    [GeneratedRegex(@"strength|for[cç]a|muscula|functional|funcional|hiit|interval|cross|core|weight|pesos|calisthen|gin[aá]sio|gym")]
    private static partial Regex StrengthPattern();
}
