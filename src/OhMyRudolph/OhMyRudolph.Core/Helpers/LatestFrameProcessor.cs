namespace OhMyRudolph.Core.Helpers;

/// <summary>Owns one active item and at most one waiting item. Posting always transfers ownership.</summary>
public sealed class LatestFrameProcessor<T> : IAsyncDisposable where T : class, IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _available = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<T, CancellationToken, Task> _process;
    private readonly Action<Exception> _onError;
    private readonly Task _worker;
    private T? _waiting;
    private bool _stopped;
    private Task? _disposeTask;

    public LatestFrameProcessor(Func<T, CancellationToken, Task> process, Action<Exception> onError)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _worker = Task.Run(RunAsync);
    }

    /// <summary>Disposes rejected/superseded items; the callback only borrows an accepted item until it returns.</summary>
    public bool TryPost(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            if (_stopped)
            {
                item.Dispose();
                return false;
            }

            T? superseded = _waiting;
            _waiting = item;
            superseded?.Dispose();
            if (superseded is null)
                _available.Release();
            return true;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                await _available.WaitAsync(_stop.Token).ConfigureAwait(false);
                T? item;
                lock (_gate)
                {
                    item = _waiting;
                    _waiting = null;
                }
                if (item is null)
                    continue;

                using (item)
                {
                    try
                    {
                        _stop.Token.ThrowIfCancellationRequested();
                        await _process(item, _stop.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _onError(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null)
                return new ValueTask(_disposeTask);
            _stopped = true;
            _waiting?.Dispose();
            _waiting = null;
            _stop.Cancel();
            _disposeTask = FinishDisposeAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task FinishDisposeAsync()
    {
        try { await _worker.ConfigureAwait(false); }
        finally
        {
            _available.Dispose();
            _stop.Dispose();
        }
    }
}
