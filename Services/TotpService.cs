using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SecureGate1.Security;

namespace SecureGate1.Services;

public class TotpService
{
    private readonly SecurityPolicyOptions _policy;

    public TotpService(IOptions<SecurityPolicyOptions> policy)
    {
        _policy = policy.Value;
    }

    public byte[] CreateSecret() => RandomNumberGenerator.GetBytes(20);

    public string BuildProvisioningUri(string accountName, string base32Secret)
    {
        var label = Uri.EscapeDataString($"SecureGate:{accountName}");
        var issuer = Uri.EscapeDataString("SecureGate");
        var secret = Uri.EscapeDataString(base32Secret);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits={_policy.TotpDigits}&period={_policy.TotpPeriodSeconds}&algorithm=SHA1";
    }

    public string ComputeCode(byte[] secret, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var mod = 1;
        for (var i = 0; i < _policy.TotpDigits; i++)
            mod *= 10;
        return (binary % mod).ToString(new string('0', _policy.TotpDigits));
    }

    public long? Verify(byte[] secret, string code, DateTimeOffset now, long? lastAcceptedTimeStep)
    {
        var normalized = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (normalized.Length != _policy.TotpDigits)
            return null;

        var step = now.ToUnixTimeSeconds() / _policy.TotpPeriodSeconds;
        long? match = null;
        for (var delta = -_policy.TotpDriftSteps; delta <= _policy.TotpDriftSteps; delta++)
        {
            var candidate = step + delta;
            if (candidate < 0)
                continue;
            if (lastAcceptedTimeStep.HasValue && candidate <= lastAcceptedTimeStep.Value)
                continue;

            var expected = ComputeCode(secret, candidate);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                match = candidate;
            }
        }

        return match;
    }
}

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        return output.ToString();
    }

    public static bool TryDecode(string input, out byte[] bytes)
    {
        bytes = [];
        var cleaned = (input ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        if (cleaned.Length == 0)
            return false;

        var output = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var character in cleaned)
        {
            var index = Alphabet.IndexOf(character);
            if (index < 0)
                return false;
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        bytes = output.ToArray();
        return bytes.Length > 0;
    }

    public static string Group(string base32)
    {
        var parts = new List<string>();
        for (var i = 0; i < base32.Length; i += 4)
            parts.Add(base32.Substring(i, Math.Min(4, base32.Length - i)));
        return string.Join(' ', parts);
    }
}
