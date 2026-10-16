using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Push.Models.DTOs;
using BodyOps.WebApi.Features.Push.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BodyOps.UnitTest.Push;

/// <summary>
/// Web Push: the encryption a browser can open (RFC 8291), the VAPID token the push services check (RFC 8292), and
/// the devices of an account.
/// </summary>
public sealed class WebPushTests : IDisposable
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-token";

    private readonly TempFolder _data = TestSupport.NewFolder();
    private readonly FakePushRepository _repository = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Encrypt_CanBeOpenedByTheBrowser()
    {
        var (browser, p256dh, auth) = Browser();
        var payload = Encoding.UTF8.GetBytes("{\"title\":\"Breakfast at 08:00\"}");

        var body = WebPushCrypto.Encrypt(payload, p256dh, auth);

        Assert.Equal(payload, Decrypt(body, browser, WebPushCrypto.FromBase64Url(auth)));
        Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
        Assert.Equal(65, body[20]);
    }

    [Fact]
    public void Encrypt_UsesAFreshKeyAndSaltEveryTime()
    {
        var (_, p256dh, auth) = Browser();
        var payload = Encoding.UTF8.GetBytes("same");

        Assert.NotEqual(WebPushCrypto.Encrypt(payload, p256dh, auth), WebPushCrypto.Encrypt(payload, p256dh, auth));
    }

    [Fact]
    public void VapidToken_IsSignedForThePushServiceOrigin()
    {
        var (publicKey, privateKey) = WebPushCrypto.CreateVapidKeys();
        var expires = DateTimeOffset.UtcNow.AddHours(12);

        var header = WebPushCrypto.VapidAuthorization(new Uri(Endpoint), "mailto:me@example.com", publicKey, privateKey, expires);

        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={publicKey}", header);
        var token = header["vapid t=".Length..header.IndexOf(',', StringComparison.Ordinal)];
        var parts = token.Split('.');
        var claims = JsonDocument.Parse(WebPushCrypto.FromBase64Url(parts[1])).RootElement;
        Assert.Equal("https://fcm.googleapis.com", claims.GetProperty("aud").GetString());
        Assert.Equal("mailto:me@example.com", claims.GetProperty("sub").GetString());
        Assert.Equal(expires.ToUnixTimeSeconds(), claims.GetProperty("exp").GetInt64());

        var point = WebPushCrypto.FromBase64Url(publicKey);
        using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..65] } });
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), WebPushCrypto.FromBase64Url(parts[2]), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x", true)]
    [InlineData("https://web.push.apple.com/QOx", true)]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/x", false)]
    [InlineData("https://fcm.googleapis.com:8443/x", false)]
    [InlineData("https://evil.example/fcm.googleapis.com", false)]
    [InlineData("https://notfcm.googleapis.com.evil.example/x", false)]
    [InlineData("https://192.168.1.10/push", false)]
    public void OnlyThePushServices_AreAccepted(string endpoint, bool expected) =>
        Assert.Equal(expected, PushSender.IsPushServiceEndpoint(endpoint));

    [Fact]
    public async Task Subscribe_RefusesOtherHostsAndBadKeys()
    {
        var service = Service(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();

        var host = await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = "https://nas.local/push", P256dh = p256dh, Auth = auth });
        var keys = await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = "abc", Auth = auth });

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, host.Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, keys.Status);
        Assert.Empty(_repository.Devices);
    }

    [Fact]
    public async Task Subscribe_TheSameEndpointTwice_KeepsOneDevice_ForTheAccountSignedIn()
    {
        var service = Service(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();

        await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth, Device = "iPhone · Milo app" });
        var state = (await service.SubscribeAsync(2, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth, Device = "iPhone · Safari" })).Result!;

        var device = Assert.Single(state.Devices);
        Assert.Equal("iPhone · Safari", device.Device);
        Assert.Equal(PushDeviceDTO.KeyOf(Endpoint), device.Key);
        Assert.Equal(12, device.Key.Length);
        Assert.Equal(65, WebPushCrypto.FromBase64Url(state.PublicKey).Length);
        Assert.Empty((await service.GetAsync(1)).Result!.Devices);
    }

    [Fact]
    public async Task Disabled_RefusesSubscribeAndTest()
    {
        var service = new PushService(_repository, Sender(HttpStatusCode.Created), TestSupport.Options(options => options.PushEnabled = false), TestSupport.Globalization);
        var (_, p256dh, auth) = Browser();

        Assert.Equal(ServiceApiResponseStatus.Unavailable, (await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth })).Status);
        Assert.Equal(ServiceApiResponseStatus.Unavailable, (await service.TestAsync(1)).Status);
        var state = (await service.GetAsync(1)).Result!;
        Assert.False(state.Enabled);
        Assert.Equal(string.Empty, state.PublicKey);
    }

    [Fact]
    public async Task Keys_AreMadeOnce_AndKept()
    {
        var first = await Sender(HttpStatusCode.Created).PublicKeyAsync();
        var second = await Sender(HttpStatusCode.Created).PublicKeyAsync();

        Assert.Equal(first, second);
        Assert.True(File.Exists(Path.Combine(_data.Path, "push-vapid.json")));
    }

    [Fact]
    public async Task Test_PostsAnEncryptedMessage_WithVapid()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = Service(handler);
        var (browser, p256dh, auth) = Browser();
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.TestAsync(1)).Status);   // no device yet
        await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        var result = (await service.TestAsync(1)).Result!;

        Assert.Equal((1, 0), (result.Reached, result.Failed));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(Endpoint, request.Uri.AbsoluteUri);
        Assert.Equal("aes128gcm", request.ContentEncoding);
        Assert.StartsWith("vapid t=", request.Authorization);
        Assert.Equal("3600", request.Ttl);
        var payload = JsonDocument.Parse(Decrypt(request.Body, browser, WebPushCrypto.FromBase64Url(auth))).RootElement;
        Assert.Equal("Milo reminders are on", payload.GetProperty("title").GetString());
        Assert.Equal("/settings", payload.GetProperty("url").GetString());
        Assert.NotNull(Assert.Single((await service.GetAsync(1)).Result!.Devices).LastSentAt);
    }

    [Fact]
    public async Task Send_ToADeviceThatIsGone_RemovesIt()
    {
        var service = Service(HttpStatusCode.Gone);
        var (_, p256dh, auth) = Browser();
        await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        var result = (await service.TestAsync(1)).Result!;

        Assert.Equal((0, 0), (result.Reached, result.Failed));
        Assert.Empty(_repository.Devices);
    }

    [Fact]
    public async Task Send_ThatFails_KeepsTheError()
    {
        var service = Service(HttpStatusCode.Forbidden);
        var (_, p256dh, auth) = Browser();
        await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        var result = (await service.TestAsync(1)).Result!;

        Assert.Equal((0, 1), (result.Reached, result.Failed));
        Assert.StartsWith("HTTP 403", Assert.Single(_repository.Devices).LastError);
    }

    [Fact]
    public async Task Unsubscribe_ById_OnlyRemovesTheAccountsOwnDevice()
    {
        var service = Service(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();
        var id = Assert.Single((await service.SubscribeAsync(1, new PushSubscribeDTO { Endpoint = Endpoint, P256dh = p256dh, Auth = auth })).Result!.Devices).Id;

        await service.UnsubscribeAsync(2, new PushUnsubscribeDTO { Id = id });
        Assert.Single(_repository.Devices);

        await service.UnsubscribeAsync(1, new PushUnsubscribeDTO { Id = id });
        Assert.Empty(_repository.Devices);
    }

    [Fact]
    public void Payload_IsCutToFitOneRecord_AndCarriesTheButtons()
    {
        var payload = PushSender.PayloadOf(new PushMessage("Title", new string('x', 10_000), "/today", "t", [new PushAction("ate", "Ate it")], new Dictionary<string, string> { ["meal"] = "4" }));

        Assert.True(payload.Length <= WebPushCrypto.MaxPayloadBytes);
        var root = JsonDocument.Parse(payload).RootElement;
        Assert.EndsWith("…", root.GetProperty("body").GetString());
        Assert.Equal("ate", root.GetProperty("actions")[0].GetProperty("action").GetString());
        Assert.Equal("4", root.GetProperty("data").GetProperty("meal").GetString());
    }

    private PushService Service(HttpStatusCode status) => Service(new RecordingHandler(status));

    private PushService Service(RecordingHandler handler) =>
        new(_repository, Sender(handler), TestSupport.Options(options => options.DataDirectory = _data.Path), TestSupport.Globalization);

    private PushSender Sender(HttpStatusCode status) => Sender(new RecordingHandler(status));

    private PushSender Sender(RecordingHandler handler) =>
        new(_repository, TestSupport.Options(options => options.DataDirectory = _data.Path), TimeProvider.System, NullLogger<PushSender>.Instance) { Http = new HttpClient(handler) };

    private static (ECDiffieHellman Key, string P256dh, string Auth) Browser()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        return (key, WebPushCrypto.Base64Url([0x04, .. q.X!, .. q.Y!]), WebPushCrypto.Base64Url(RandomNumberGenerator.GetBytes(16)));
    }

    /// <summary>What the browser does with a push body (RFC 8291 from the receiving side).</summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman browser, byte[] authSecret)
    {
        var salt = body[..16];
        var idLength = body[20];
        var serverPublic = body[21..(21 + idLength)];
        var ciphertext = body[(21 + idLength)..^16];
        var tag = body[^16..];

        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
        });
        var q = browser.ExportParameters(false).Q;
        byte[] browserPublic = [0x04, .. q.X!, .. q.Y!];
        var shared = browser.DeriveRawSecretAgreement(server.PublicKey);
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. browserPublic, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, info);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        Assert.Equal(0x02, plaintext[^1]);
        return plaintext[..^1];
    }

    /// <summary>Answers every request with one status and keeps what was sent.</summary>
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(Uri Uri, byte[] Body, string ContentEncoding, string Authorization, string Ttl)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                string.Join(",", request.Content.Headers.ContentEncoding),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.First() : string.Empty,
                request.Headers.TryGetValues("TTL", out var ttl) ? ttl.First() : string.Empty));
            return new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.Forbidden ? "bad jwt" : string.Empty) };
        }
    }
}
