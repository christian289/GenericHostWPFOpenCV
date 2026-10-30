namespace OhMyRudolph.Core.Helpers;

/// <summary>Decides snapshot eligibility using the original successful read time, never publication time.</summary>
public static class CaptureFreshness
{
    /// <summary>Timestamps are monotonic Stopwatch ticks. Exactly one second is still eligible.</summary>
    public static bool CanCapture(bool isCameraRunning, bool isCurrentGeneration,
        long capturedTimestamp, long nowTimestamp) =>
        isCameraRunning && isCurrentGeneration && nowTimestamp >= capturedTimestamp &&
        (ulong)(nowTimestamp - capturedTimestamp) <= (ulong)Stopwatch.Frequency;
}
