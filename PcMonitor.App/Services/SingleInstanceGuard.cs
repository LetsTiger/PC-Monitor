namespace PcMonitor.App.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _replacementRequest;
    private readonly EventWaitHandle _replacementApproved;
    private readonly EventWaitHandle _replacementRejected;
    private readonly CancellationTokenSource _listenerCancellation = new();
    private Task? _listenerTask;
    private bool _ownsMutex;

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(true, name, out _ownsMutex);
        _replacementRequest = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".ReplacementRequest");
        _replacementApproved = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".ReplacementApproved");
        _replacementRejected = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".ReplacementRejected");
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public void StartReplacementListener(Func<bool> confirmReplacement, Action shutdown)
    {
        ArgumentNullException.ThrowIfNull(confirmReplacement);
        ArgumentNullException.ThrowIfNull(shutdown);
        if (!_ownsMutex)
        {
            throw new InvalidOperationException("Nur die primäre Instanz kann Übergabeanforderungen empfangen.");
        }

        if (_listenerTask is not null) return;

        _listenerTask = Task.Run(() =>
        {
            WaitHandle[] waitHandles = [_replacementRequest, _listenerCancellation.Token.WaitHandle];
            while (WaitHandle.WaitAny(waitHandles) == 0)
            {
                bool approved = confirmReplacement();
                if (approved)
                {
                    _replacementApproved.Set();
                    shutdown();
                    return;
                }

                _replacementRejected.Set();
            }
        });
    }

    public bool RequestReplacement(TimeSpan responseTimeout, TimeSpan mutexTimeout)
    {
        if (_ownsMutex) return true;

        _replacementApproved.Reset();
        _replacementRejected.Reset();
        _replacementRequest.Set();

        int response = WaitHandle.WaitAny(
            [_replacementApproved, _replacementRejected],
            responseTimeout);
        if (response != 0) return false;

        try
        {
            _ownsMutex = _mutex.WaitOne(mutexTimeout);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        return _ownsMutex;
    }

    public void Dispose()
    {
        _listenerCancellation.Cancel();
        if (_listenerTask is not null && Task.CurrentId != _listenerTask.Id)
        {
            try
            {
                _listenerTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // Der Listener wird beim Beenden nicht weiter benötigt.
            }
        }

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _replacementRequest.Dispose();
        _replacementApproved.Dispose();
        _replacementRejected.Dispose();
        _listenerCancellation.Dispose();
        _mutex.Dispose();
    }
}
