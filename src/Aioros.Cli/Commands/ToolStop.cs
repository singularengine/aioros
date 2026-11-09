using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aioros.Cli.Commands;

/// <summary>While alive, a signal that stops the tool stops the child and every process under it instead, so the tool exits with nothing left behind.</summary>
public sealed class ToolStop : IDisposable
{
    private readonly Process _child;
    private readonly PosixSignalRegistration[] _signals;

    public ToolStop(Process child)
    {
        _child = child;
        _signals = [PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop), PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop), PosixSignalRegistration.Create(PosixSignal.SIGHUP, Stop)];
    }

    public void Dispose()
    {
        foreach (var signal in _signals)
        {
            signal.Dispose();
        }
    }

    private void Stop(PosixSignalContext context)
    {
        context.Cancel = true;
        try
        {
            _child.Kill(entireProcessTree: true);
        }
        catch (Exception failure) when (failure is InvalidOperationException or AggregateException)
        {
            return;
        }
    }
}
