using System.IO;
using System.IO.Pipes;
using System.Text;

namespace QuickCut.Capture.Services;

public sealed class SingleInstanceCommandServer : IDisposable
{
    private static readonly TimeSpan DefaultClientTimeout = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);
    private const int MaxSendAttempts = 4;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _listenTask;
    private bool _disposed;

    public SingleInstanceCommandServer()
    {
        _listenTask = Task.Run(ListenAsync);
    }

    public event Action<QuickCutSingleInstanceCommand>? CommandReceived;

    public static bool TrySend(QuickCutSingleInstanceCommand command)
    {
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            if (TrySendOnce(command))
            {
                return true;
            }

            if (attempt < MaxSendAttempts)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        return false;
    }

    private static bool TrySendOnce(QuickCutSingleInstanceCommand command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                QuickCutInstanceNames.MainPipeName,
                PipeDirection.Out);
            pipe.Connect((int)DefaultClientTimeout.TotalMilliseconds);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: false)
            {
                AutoFlush = true,
            };
            writer.WriteLine(command.ToPayload());
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            _listenTask.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
        _disposed = true;
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    QuickCutInstanceNames.MainPipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);

                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: false);
                var payload = await reader.ReadLineAsync(_cancellation.Token).ConfigureAwait(false);
                if (QuickCutSingleInstanceCommand.TryParse(payload, out var command))
                {
                    CommandReceived?.Invoke(command);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
            }
        }
    }
}
