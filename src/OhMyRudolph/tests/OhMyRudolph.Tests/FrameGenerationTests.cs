using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Tests;

public sealed class FrameGenerationTests
{
    [Fact]
    public void SwitchingCameraRejectsLateResponseFromPreviousCamera()
    {
        var generation = new FrameGeneration();
        var old = generation.Begin("camera-0");
        var current = generation.Begin("camera-1");
        int published = 0;

        Assert.False(generation.TryRunCurrent(old, () => published = -1));
        Assert.True(generation.TryRunCurrent(current, () => published = 1));
        Assert.Equal(1, published);
    }

    [Fact]
    public void StopThenStartSameCameraRejectsPreviousSession()
    {
        var generation = new FrameGeneration();
        var old = generation.Begin("camera-0");
        generation.Invalidate();
        Assert.False(generation.IsCurrent(old));
        var restarted = generation.Begin("camera-0");

        Assert.False(generation.TryRunCurrent(old, () => throw new InvalidOperationException()));
        Assert.True(generation.IsCurrent(restarted));
    }

    [Fact]
    public void CameraIdentityMustMatchEvenWhenGenerationNumberMatches()
    {
        var generation = new FrameGeneration();
        var current = generation.Begin("camera-0");

        Assert.False(generation.TryRunCurrent(current with { CameraId = "camera-1" }, () => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task DelayedResponseCannotPublishAfterInvalidationReturns()
    {
        var generation = new FrameGeneration();
        var stamp = generation.Begin("camera-0");
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool published = false;
        var task = Task.Run(async () =>
        {
            await response.Task;
            return generation.TryRunCurrent(stamp, () => published = true);
        });
        generation.Invalidate();
        response.SetResult();

        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(published);
    }
}
