using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BodyOps.WebApi.Features.Push.Services;

/// <summary>
/// The two pieces of the Web Push protocol, with the .NET crypto only (no package): the message encryption of
/// RFC 8291 ("aes128gcm", RFC 8188) and the VAPID authorization of RFC 8292 (an ES256 JWT signed with the
/// server's P-256 key).
/// </summary>
internal static class WebPushCrypto
{
    /// <summary>The record size written in the header; one record holds the whole message.</summary>
    private const int RecordSize = 4096;

    /// <summary>Longest plaintext that fits in one record (record size − 16-byte tag − 1 delimiter byte).</summary>
    internal const int MaxPayloadBytes = RecordSize - 16 - 1;

    /// <summary>
    /// Creates a new VAPID key pair.
    /// </summary>
    /// <returns>The public key (65-byte uncompressed point) and the private key (32-byte scalar), base64url.</returns>
    public static (string PublicKey, string PrivateKey) CreateVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);
        return (Base64Url(UncompressedPoint(parameters.Q)), Base64Url(parameters.D!));
    }

    /// <summary>
    /// Encrypts a message for one subscription (RFC 8291 with a fresh key pair and salt per message).
    /// </summary>
    /// <param name="payload">The plaintext (at most <see cref="MaxPayloadBytes"/> bytes).</param>
    /// <param name="p256dh">The subscription's public key (base64url, 65-byte uncompressed point).</param>
    /// <param name="auth">The subscription's authentication secret (base64url, 16 bytes).</param>
    /// <returns>The request body: header (salt, record size, key id = our public key) then the ciphertext.</returns>
    public static byte[] Encrypt(byte[] payload, string p256dh, string auth) =>
        Encrypt(payload, p256dh, auth, RandomNumberGenerator.GetBytes(16), ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256));

    /// <summary>Encrypts with a given salt and server key pair (tests use fixed ones).</summary>
    internal static byte[] Encrypt(byte[] payload, string p256dh, string auth, byte[] salt, ECDiffieHellman serverKey)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, MaxPayloadBytes);

        using (serverKey)
        {
            var userAgentPublic = FromBase64Url(p256dh);
            var authSecret = FromBase64Url(auth);
            if (userAgentPublic.Length != 65 || userAgentPublic[0] != 0x04 || authSecret.Length < 16)
            {
                throw new ArgumentException("The subscription keys are not a P-256 public key and a 16-byte secret.");
            }

            using var userAgentKey = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = userAgentPublic[1..33], Y = userAgentPublic[33..65] },
            });
            var serverPublic = UncompressedPoint(serverKey.ExportParameters(includePrivateParameters: false).Q);
            var sharedSecret = serverKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

            // RFC 8291 §3.4: IKM from the ECDH secret and the auth secret, bound to both public keys.
            var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), userAgentPublic, serverPublic);
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

            // RFC 8188 §2.2: content key and nonce from the salt.
            var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

            // One record: the payload, then the 0x02 "last record" delimiter.
            var plaintext = new byte[payload.Length + 1];
            payload.CopyTo(plaintext, 0);
            plaintext[^1] = 0x02;

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(contentKey, tag.Length))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var header = new byte[16 + 4 + 1];
            salt.CopyTo(header, 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
            header[20] = (byte)serverPublic.Length;

            return Concat(header, serverPublic, ciphertext, tag);
        }
    }

    /// <summary>
    /// The Authorization header value for one push service: "vapid t=&lt;JWT&gt;, k=&lt;public key&gt;".
    /// </summary>
    /// <param name="endpoint">The subscription endpoint (its origin is the JWT audience).</param>
    /// <param name="subject">A contact for the push service: "mailto:…" or "https://…".</param>
    /// <param name="publicKey">The VAPID public key (base64url).</param>
    /// <param name="privateKey">The VAPID private key (base64url).</param>
    /// <param name="expires">When the token expires (at most 24 h ahead).</param>
    /// <returns>The header value.</returns>
    public static string VapidAuthorization(Uri endpoint, string subject, string publicKey, string privateKey, DateTimeOffset expires)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["typ"] = "JWT", ["alg"] = "ES256" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = expires.ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));

        var point = FromBase64Url(publicKey);
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });

        // JWS ES256 wants the raw r||s signature, which is .NET's default (IEEE P1363) format.
        var signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256);
        return $"vapid t={header}.{claims}.{Base64Url(signature)}, k={publicKey}";
    }

    /// <summary>Base64url without padding.</summary>
    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Base64url (with or without padding, or plain base64) to bytes.</summary>
    internal static byte[] FromBase64Url(string text)
    {
        var base64 = text.Trim().Replace('-', '+').Replace('_', '/').TrimEnd('=');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    private static byte[] UncompressedPoint(ECPoint point) => Concat([0x04], point.X!, point.Y!);

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(static part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
