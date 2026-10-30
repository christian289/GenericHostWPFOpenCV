using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Tests;

public sealed class LatestFrameProcessorTests
{
    [Fact]
    public async Task SlowConsumerKeepsOnlyNewestWaitingFrameAndDisposesSupersededFrames()
    {
        var entered = Signal();
        var release = Signal();
        var latestProcessed = Signal();
        var seen = new List<int>();
        var first = new OwnedFrame(1);
        var dropped = new OwnedFrame(2);
        var latest = new OwnedFrame(3);
        await using var processor = new LatestFrameProcessor<OwnedFrame>(async (frame, token) =>
        {
            seen.Add(frame.Id);
            if (frame.Id == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
            else
            {
                latestProcessed.SetResult();
            }
        }, _ => throw new InvalidOperationException("Unexpected processing error."));

        Assert.True(processor.TryPost(first));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(processor.TryPost(dropped));
        Assert.True(processor.TryPost(latest));
        Assert.Equal(1, dropped.DisposeCount);
        Assert.Equal(0, first.DisposeCount);
        Assert.Equal(0, latest.DisposeCount);

        release.SetResult();
        await latestProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await processor.DisposeAsync();

        Assert.Equal(new[] { 1, 3 }, seen);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, latest.DisposeCount);
    }

    [Fact]
    public async Task StopCancelsCurrentFrameDisposesWaitingFrameAndRejectsFurtherOwnership()
    {
        var entered = Signal();
        var first = new OwnedFrame(1);
        var waiting = new OwnedFrame(2);
        var rejected = new OwnedFrame(3);
        var processor = new LatestFrameProcessor<OwnedFrame>(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, _ => throw new InvalidOperationException("Cancellation is not an error."));
        processor.TryPost(first);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        processor.TryPost(waiting);

        await processor.DisposeAsync();
        await processor.DisposeAsync();

        Assert.False(processor.TryPost(rejected));
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, waiting.DisposeCount);
        Assert.Equal(1, rejected.DisposeCount);
    }

    [Fact]
    public async Task FailedFrameIsDisposedWithoutCompletingReusableConsumer()
    {
        var failureObserved = Signal();
        var recovered = Signal();
        var first = new OwnedFrame(1);
        var second = new OwnedFrame(2);
        await using var processor = new LatestFrameProcessor<OwnedFrame>((frame, _) =>
        {
            if (frame.Id == 1)
                throw new IOException("Synthetic frame failure.");
            recovered.SetResult();
            return Task.CompletedTask;
        }, _ => failureObserved.SetResult());

        processor.TryPost(first);
        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        processor.TryPost(second);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await processor.DisposeAsync();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task ConsumerNeverOverlapsEvenWhenManyFramesArriveDuringAwait()
    {
        var entered = Signal();
        var release = Signal();
        int active = 0;
        int maximumActive = 0;
        var frames = Enumerable.Range(1, 100).Select(id => new OwnedFrame(id)).ToArray();
        await using var processor = new LatestFrameProcessor<OwnedFrame>(async (_, token) =>
        {
            int count = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, count);
            entered.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            finally { Interlocked.Decrement(ref active); }
        }, _ => throw new InvalidOperationException());
        processor.TryPost(frames[0]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        foreach (var frame in frames.Skip(1))
            processor.TryPost(frame);
        release.SetResult();
        await processor.DisposeAsync();

        Assert.Equal(1, maximumActive);
        Assert.All(frames, frame => Assert.Equal(1, frame.DisposeCount));
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class OwnedFrame(int id) : IDisposable
    {
        public int Id { get; } = id;
        public int DisposeCount;
        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }
}
