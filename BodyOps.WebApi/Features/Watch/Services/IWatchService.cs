using System.Text.Json;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Watch.Models.DTOs;

namespace BodyOps.WebApi.Features.Watch.Services;

/// <summary>
/// Apple Watch and Apple Health through an iOS Shortcut: the personal key, and what the Shortcut sends.
/// </summary>
internal interface IWatchService
{
    /// <summary>
    /// The key (hint only) and the workouts of the last four weeks.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<WatchDTO>> GetAsync(long userId);

    /// <summary>
    /// Makes a new key, replacing the old one (which stops working).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key, shown once, and the state.</returns>
    Task<ServiceApiResponse<WatchKeyDTO>> CreateKeyAsync(long userId);

    /// <summary>
    /// Revokes the key. The workouts already sent stay.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<WatchDTO>> RevokeKeyAsync(long userId);

    /// <summary>
    /// Imports what the Shortcut sent: a workout (matched to the day's session or planned cardio) and/or a weigh-in.
    /// </summary>
    /// <param name="key">The key from the request header, or null to look for it in the body.</param>
    /// <param name="body">The JSON body.</param>
    /// <returns>What was imported, in words.</returns>
    Task<ServiceApiResponse<WatchIngestResultDTO>> IngestAsync(string? key, JsonElement body);
}
