using System.Diagnostics;
using OhMyRudolph.ProcessingHost;

namespace OhMyRudolph.ProcessingHost.Tests;

public sealed class OwnerLeaseTests
{
    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(31, true)]
    public void MissingOwnerExpiresAtTheDeadline(int seconds, bool expected)
    {
        long now = 0;
        var lease = new OwnerLease(TimeSpan.FromSeconds(30), () => now);
        now = seconds * Stopwatch.Frequency;
        Assert.Equal(expected, lease.HasExpired);
    }

    [Fact]
    public void RenewingTheOwnerExtendsItsDeadline()
    {
        long now = 0;
        var lease = new OwnerLease(TimeSpan.FromSeconds(30), () => now);
        now = 20 * Stopwatch.Frequency;
        lease.Renew();
        now = 40 * Stopwatch.Frequency;
        Assert.False(lease.HasExpired);
        now = 50 * Stopwatch.Frequency;
        Assert.True(lease.HasExpired);
    }
}
