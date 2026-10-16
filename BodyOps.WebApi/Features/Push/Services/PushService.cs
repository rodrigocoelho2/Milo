using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Push.Models.DTOs;
using BodyOps.WebApi.Features.Push.Repositories;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Push.Services;

/// <summary>
/// Push reminders for the signed-in account. Only the browsers' own push services are accepted as endpoints.
/// </summary>
/// <param name="pushRepository">Devices.</param>
/// <param name="pushSender">Sends notifications.</param>
/// <param name="options">The app options (push on or off).</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class PushService(IPushRepository pushRepository, IPushSender pushSender, IOptions<BodyOpsOptions> options, IGlobalizationHelper globalizationHelper) : IPushService
{
    /// <summary>The longest device label kept.</summary>
    public const int MaxDevice = 60;

    /// <summary>The longest endpoint accepted.</summary>
    public const int MaxEndpoint = 800;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> GetAsync(long userId) => ServiceApiResponse<PushDTO>.Success(await StateAsync(userId));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> SubscribeAsync(long userId, PushSubscribeDTO request)
    {
        if (!options.Value.PushEnabled)
        {
            return ServiceApiResponse<PushDTO>.Unavailable(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.PushDisabled)));
        }

        var endpoint = request.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length > MaxEndpoint || !PushSender.IsPushServiceEndpoint(endpoint))
        {
            return await globalizationHelper.ValidationProblemAsync<PushDTO>("endpoint", nameof(TranslationKeys.PushEndpointNotAllowed));
        }

        if (!IsKey(request.P256dh, 65) || !IsKey(request.Auth, 16))
        {
            return await globalizationHelper.ValidationProblemAsync<PushDTO>("keys", nameof(TranslationKeys.PushKeysInvalid));
        }

        var device = (request.Device ?? string.Empty).Trim();
        await pushRepository.SaveAsync(userId, endpoint, request.P256dh!.Trim(), request.Auth!.Trim(), device.Length <= MaxDevice ? device : device[..MaxDevice]);
        return ServiceApiResponse<PushDTO>.Success(await StateAsync(userId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> UnsubscribeAsync(long userId, PushUnsubscribeDTO request)
    {
        var endpoint = string.IsNullOrWhiteSpace(request.Endpoint) ? null : request.Endpoint.Trim();
        if (request.Id is { } id)
        {
            await pushRepository.DeleteAsync(userId, id, null);
        }

        if (endpoint is not null)
        {
            await pushRepository.DeleteAsync(null, null, endpoint);
        }

        return ServiceApiResponse<PushDTO>.Success(await StateAsync(userId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushTestResultDTO>> TestAsync(long userId)
    {
        if (!options.Value.PushEnabled)
        {
            return ServiceApiResponse<PushTestResultDTO>.Unavailable(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.PushDisabled)));
        }

        var devices = await pushRepository.ListAsync(userId);
        if (devices.Count == 0)
        {
            return await globalizationHelper.ValidationProblemAsync<PushTestResultDTO>("devices", nameof(TranslationKeys.PushNoDevices));
        }

        var (reached, failed) = await pushSender.SendAsync(devices, [new PushMessage("Milo reminders are on", "This is how a reminder looks on this device.", "/settings", "test")]);
        return ServiceApiResponse<PushTestResultDTO>.Success(new PushTestResultDTO { Reached = reached, Failed = failed });
    }

    /// <summary>
    /// Whether a subscription key is base64url of the right length (65-byte uncompressed P-256 point, 16-byte secret).
    /// </summary>
    /// <param name="text">The key.</param>
    /// <param name="length">The byte length.</param>
    /// <returns>True when valid.</returns>
    internal static bool IsKey(string? text, int length)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            return false;
        }

        try
        {
            var bytes = WebPushCrypto.FromBase64Url(text.Trim());
            return bytes.Length == length && (length != 65 || bytes[0] == 0x04);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<PushDTO> StateAsync(long userId) => new()
    {
        Enabled = options.Value.PushEnabled,
        PublicKey = options.Value.PushEnabled ? await pushSender.PublicKeyAsync() : string.Empty,
        Devices = [.. (await pushRepository.ListAsync(userId)).OrderByDescending(static row => row.CreatedAt).Select(static row => new PushDeviceDTO(row))],
    };
}
