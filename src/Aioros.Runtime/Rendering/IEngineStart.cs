namespace Aioros.Runtime.Rendering;

/// <summary>
/// A page engine that starts on demand (a production app whose plans cover its pages): starting it brings up Avalonia's
/// platform with its UI dispatcher on the engine's own thread. A page whose models create Avalonia objects starts it
/// before they are created, wherever they are created, since such an object needs the platform and the first one a thread
/// creates would take Avalonia's UI dispatcher for that thread.
/// </summary>
public interface IEngineStart
{
    /// <summary>Whether the engine has been started.</summary>
    bool Started { get; }

    /// <summary>Starts the engine, or returns at once when it runs.</summary>
    ValueTask StartAsync(CancellationToken cancellationToken);
}
