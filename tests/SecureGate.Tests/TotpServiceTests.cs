using System.Text;
using Microsoft.Extensions.Options;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class TotpServiceTests
{
    private readonly TotpService _totp = new(Options.Create(new SecurityPolicyOptions()));
    private readonly byte[] _secret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void ComputeCode_MatchesRfc6238SixDigits(long unixTime, string expected)
    {
        var step = unixTime / 30;
        Assert.Equal(expected, _totp.ComputeCode(_secret, step));
    }

    [Fact]
    public void Verify_AcceptsOneStepOfClockDrift_AndRejectsTwo()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111109);
        var current = _totp.ComputeCode(_secret, 1111111109 / 30);
        var previous = _totp.ComputeCode(_secret, (1111111109 / 30) - 1);
        var tooOld = _totp.ComputeCode(_secret, (1111111109 / 30) - 2);

        Assert.NotNull(_totp.Verify(_secret, current, now, null));
        Assert.NotNull(_totp.Verify(_secret, previous, now, null));
        Assert.Null(_totp.Verify(_secret, tooOld, now, null));
    }

    [Fact]
    public void Verify_RejectsACodeThatWasAlreadyAccepted()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(59);
        var matched = _totp.Verify(_secret, "287082", now, null);
        Assert.Equal(1, matched);
        Assert.Null(_totp.Verify(_secret, "287082", now, matched));
    }

    [Fact]
    public void Verify_RejectsNonNumericAndWrongLengthCodes()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(59);
        Assert.Null(_totp.Verify(_secret, "abcdef", now, null));
        Assert.Null(_totp.Verify(_secret, "12345", now, null));
    }
}
