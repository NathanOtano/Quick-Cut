namespace QuickCut.Capture.Services;

public sealed class SingleInstanceLock : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceLock(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        _ownsMutex = createdNew;
        IsPrimaryInstance = createdNew;
    }

    public bool IsPrimaryInstance { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _disposed = true;
    }
}
