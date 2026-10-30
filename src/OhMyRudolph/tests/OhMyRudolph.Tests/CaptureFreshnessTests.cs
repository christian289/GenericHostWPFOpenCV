using System.Diagnostics;
using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Tests;

public sealed class CaptureFreshnessTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(250, true)]
    [InlineData(999, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    [InlineData(5000, false)]
    [InlineData(-1, false)]
    public void OnlyCaptureAgesFromZeroThroughOneSecondAreEligible(int ageMilliseconds, bool expected)
    {
        long capturedAt = 10 * Stopwatch.Frequency;
        long now = capturedAt + ageMilliseconds * Stopwatch.Frequency / 1000;

        Assert.Equal(expected, CaptureFreshness.CanCapture(true, true, capturedAt, now));
    }

    [Fact]
    public void OneStopwatchTickPastOneSecondIsAlreadyExpired()
    {
        long capturedAt = 10 * Stopwatch.Frequency;

        Assert.False(CaptureFreshness.CanCapture(true, true, capturedAt, capturedAt + Stopwatch.Frequency + 1));
    }

    [Fact]
    public void ReadFailureRejectsEvenARecentlyCapturedCurrentGenerationFrame()
    {
        long capturedAt = 10 * Stopwatch.Frequency;

        Assert.False(CaptureFreshness.CanCapture(false, true, capturedAt, capturedAt + Stopwatch.Frequency / 4));
    }

    [Fact]
    public void StoppedOrSwitchedGenerationRejectsEvenAnOtherwiseFreshFrame()
    {
        long capturedAt = 10 * Stopwatch.Frequency;

        Assert.False(CaptureFreshness.CanCapture(true, false, capturedAt, capturedAt + Stopwatch.Frequency / 4));
    }

    [Fact]
    public void InferenceAndQueueDelayRemainPartOfAgeEvenWhenFrameHasJustBeenPublished()
    {
        long capturedAt = 10 * Stopwatch.Frequency;
        long publishedAt = capturedAt + 2 * Stopwatch.Frequency;
        long captureRequestedAt = publishedAt + Stopwatch.Frequency / 4;

        Assert.False(CaptureFreshness.CanCapture(true, true, capturedAt, captureRequestedAt));
    }
}
