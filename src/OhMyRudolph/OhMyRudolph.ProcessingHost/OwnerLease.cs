using System.Diagnostics;

namespace OhMyRudolph.ProcessingHost;

public sealed class OwnerLease
{
    private readonly Func<long> _timestamp;
    private readonly TimeSpan _timeout;
    private long _lastSeen;

    public OwnerLease(TimeSpan timeout, Func<long>? timestamp = null)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _timeout = timeout;
        _lastSeen = _timestamp();
    }

    public void Renew() => Interlocked.Exchange(ref _lastSeen, _timestamp());
    public bool HasExpired => Stopwatch.GetElapsedTime(Volatile.Read(ref _lastSeen), _timestamp()) >= _timeout;
}

internal sealed class OwnerLeaseMonitor : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _monitor;

    public OwnerLeaseMonitor(OwnerLease lease) => _monitor = MonitorAsync(lease);

    private async Task MonitorAsync(OwnerLease lease)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                if (!lease.HasExpired) continue;
                Console.Error.WriteLine("Processing host owner lease expired.");
                // A native call cannot be safely cancelled. Terminate only this worker;
                // Podman's --rm removes its container even if the WPF owner crashed.
                Environment.Exit(73);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _monitor;
        _stop.Dispose();
    }
}
