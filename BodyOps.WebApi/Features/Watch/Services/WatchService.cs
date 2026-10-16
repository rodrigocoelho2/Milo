using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Watch.Models.DTOs;
using BodyOps.WebApi.Features.Watch.Models.Entities;
using BodyOps.WebApi.Features.Watch.Repositories;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.WebApi.Features.Watch.Services;

/// <summary>
/// Apple Watch and Apple Health. A web app can't read Health, so an iOS Shortcut on the phone sends Milo the workout
/// when it ends on the Watch, and the latest weight every morning, authenticated with a personal key (only its
/// SHA-256 is stored). Workouts go with the day's session; a walk, run or ride fills in the matching planned cardio
/// when it wasn't logged yet. Calories burned never touch the day score.
/// </summary>
/// <param name="watchRepository">Keys and workouts.</param>
/// <param name="dayService">The day (planned cardio) and logging it.</param>
/// <param name="weightService">Weigh-ins.</param>
/// <param name="clock">The app clock.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class WatchService(
    IWatchRepository watchRepository,
    IDayService dayService,
    IWeightService weightService,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : IWatchService
{
    /// <summary>What every key starts with (so it is recognisable when pasted).</summary>
    public const string KeyPrefix = "milo_hk_";

    /// <summary>How many days of workouts the state carries.</summary>
    public const int RecentDays = 28;

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WatchDTO>> GetAsync(long userId) => ServiceApiResponse<WatchDTO>.Success(await StateAsync(userId));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WatchKeyDTO>> CreateKeyAsync(long userId)
    {
        var key = KeyPrefix + Base64Url(RandomNumberGenerator.GetBytes(24));
        await watchRepository.SaveKeyAsync(userId, Hash(key), Hint(key));
        return ServiceApiResponse<WatchKeyDTO>.Success(new WatchKeyDTO { Key = key, Watch = await StateAsync(userId) });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WatchDTO>> RevokeKeyAsync(long userId)
    {
        await watchRepository.DeleteKeyAsync(userId);
        return ServiceApiResponse<WatchDTO>.Success(await StateAsync(userId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WatchIngestResultDTO>> IngestAsync(string? key, JsonElement body)
    {
        key = (key ?? (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("key", out var inBody) && inBody.ValueKind == JsonValueKind.String ? inBody.GetString() : null))?.Trim();
        if (key is not { Length: > 0 } || !key.StartsWith(KeyPrefix, StringComparison.Ordinal) || key.Length > 100
            || await watchRepository.UseKeyAsync(Hash(key)) is not { } owner)
        {
            return ServiceApiResponse<WatchIngestResultDTO>.Unauthorized(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.WatchKeyInvalid)));
        }

        var userId = owner.UserID;
        var payload = WatchPayload.Parse(body, clock.Zone, clock.Now);
        if (payload.Workout is null && payload.Weight is null)
        {
            await watchRepository.MarkKeyAsync(userId, payload.Error ?? string.Empty);
            return ServiceApiResponse<WatchIngestResultDTO>.ValidationProblems(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.WatchPayloadInvalid)))
                .AddError("body", payload.Error ?? string.Empty);
        }

        var result = new WatchIngestResultDTO();
        var parts = new List<string>();
        if (payload.Workout is { } workout)
        {
            var (saved, filled) = await SaveWorkoutAsync(userId, workout);
            result.Workout = new WatchWorkoutDTO(saved);
            result.FilledCardio = filled;
            parts.Add($"Milo got {workout.Kind}: {Summary(saved)}.");
            if (filled is not null)
            {
                parts.Add($"{filled} filled in.");
            }
        }

        if (payload.Weight is { } weight)
        {
            parts.Add(await SaveWeightAsync(userId, weight, result));
        }

        if (payload.Error is { } partial)
        {
            parts.Add(partial);
        }

        await watchRepository.MarkKeyAsync(userId, payload.Error ?? string.Empty);
        result.Message = string.Join(' ', parts);
        return ServiceApiResponse<WatchIngestResultDTO>.Success(result);
    }

    /// <summary>
    /// The planned cardio a workout fills in: the first not-yet-logged cardio of the day whose kind matches, best match first.
    /// </summary>
    /// <param name="kind">The Watch's workout type.</param>
    /// <param name="exercises">The day's exercises.</param>
    /// <returns>The exercise, or null.</returns>
    internal static DayExerciseDTO? CardioToFill(string kind, IReadOnlyList<DayExerciseDTO> exercises)
    {
        if (WatchPayload.Category(kind) != WatchCategory.Cardio)
        {
            return null;
        }

        foreach (var wanted in WatchPayload.CardioKinds(kind))
        {
            var match = exercises.FirstOrDefault(exercise => exercise.Type == ExerciseType.Cardio && (exercise.CardioKind ?? CardioKind.Other) == wanted && (exercise.CardioMinutes ?? 0) == 0 && (exercise.CardioKm ?? 0) == 0);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>"58 min · 412 kcal · 128 bpm avg".</summary>
    /// <param name="workout">The workout.</param>
    /// <returns>The summary.</returns>
    internal static string Summary(WatchWorkoutRow workout)
    {
        var parts = new List<string> { $"{Math.Max(1, (int)Math.Round(workout.DurationSeconds / 60.0))} min" };
        if (workout.ActiveKcal is { } kcal)
        {
            parts.Add($"{Math.Round(kcal).ToString("#,0", English)} kcal");
        }

        if (workout.AvgBpm is { } avg)
        {
            parts.Add($"{avg} bpm avg");
        }

        if (workout.DistanceKm is { } km)
        {
            parts.Add($"{km.ToString("0.##", English)} km");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The SHA-256 of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>32 bytes.</returns>
    internal static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    /// <summary>The start and end of a key, to recognise it ("milo_hk_7Q2m…3f9a").</summary>
    /// <param name="key">The key.</param>
    /// <returns>The hint.</returns>
    internal static string Hint(string key) => key.Length <= 16 ? key : $"{key[..(KeyPrefix.Length + 4)]}…{key[^4..]}";

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<(WatchWorkoutRow Saved, string? Filled)> SaveWorkoutAsync(long userId, WatchWorkoutData workout)
    {
        var date = DateOnly.FromDateTime(workout.StartedAt);
        var row = new WatchWorkoutRow
        {
            DayDate = date.ToDateTime(TimeOnly.MinValue),
            StartedAt = workout.StartedAt,
            EndedAt = workout.EndedAt,
            Kind = workout.Kind,
            DurationSeconds = workout.DurationSeconds,
            ActiveKcal = workout.ActiveKcal,
            AvgBpm = (short?)workout.AvgBpm,
            MaxBpm = (short?)workout.MaxBpm,
            DistanceKm = workout.DistanceKm,
            HeartRates = string.Join(',', workout.HeartRates),
        };

        // Sent again (the automation can run twice)? Then the cardio was already filled in.
        var existing = (await watchRepository.ListWorkoutsAsync(userId, date, date)).FirstOrDefault(other => other.StartedAt == workout.StartedAt);
        string? filled = null;
        if (existing?.PlanExerciseID is null && date <= clock.Today
            && await dayService.GetWeekAsync(userId, date) is { IsSuccess: true, Result: { } week }
            && week.Days.FirstOrDefault(day => day.Date == date) is { } day
            && CardioToFill(workout.Kind, day.Exercises) is { } cardio)
        {
            var minutes = Math.Max(1, Math.Round(workout.DurationSeconds / 60m));
            var log = new ExerciseLogDTO { CardioMinutes = minutes, CardioKm = cardio.CardioDistance ? workout.DistanceKm : null };
            if ((await dayService.LogExerciseAsync(userId, date, cardio.PlanExerciseId, log)).IsSuccess)
            {
                row.PlanExerciseID = cardio.PlanExerciseId;
                filled = $"{cardio.Name}, {minutes.ToString("0", English)} min" + (log.CardioKm is { } km ? $" · {km.ToString("0.##", English)} km" : string.Empty);
            }
        }

        return (await watchRepository.SaveWorkoutAsync(userId, row), filled);
    }

    private async Task<string> SaveWeightAsync(long userId, WatchWeightData weight, WatchIngestResultDTO result)
    {
        var day = weight.Date.ToString("d MMM", English);
        var weights = await weightService.ListAsync(userId);
        if (weights.Result?.Entries.Any(entry => entry.Date == weight.Date) == true)
        {
            return $"The weigh-in for {day} was already in Milo, so it stays.";
        }

        var saved = await weightService.SaveAsync(userId, weight.Date, new WeightSaveDTO { WeightKg = weight.WeightKg });
        if (!saved.IsSuccess)
        {
            return saved.Problems?.Detail ?? "The weight wasn't saved.";
        }

        result.WeightKg = weight.WeightKg;
        return $"Weight {weight.WeightKg.ToString("0.0", English)} kg saved for {day}.";
    }

    private async Task<WatchDTO> StateAsync(long userId)
    {
        var key = await watchRepository.GetKeyAsync(userId);
        var today = clock.Today;
        var workouts = await watchRepository.ListWorkoutsAsync(userId, today.AddDays(-(RecentDays - 1)), today);
        return new WatchDTO
        {
            Connected = key is not null,
            KeyHint = key?.KeyHint,
            KeyCreatedAt = key?.CreatedAt,
            LastSyncAt = key?.LastUsedAt,
            LastError = key?.LastError ?? string.Empty,
            Workouts = [.. workouts.Select(static row => new WatchWorkoutDTO(row))],
        };
    }
}
