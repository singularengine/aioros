using Aioros.Pages;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// A page engine that renders on a thread of its own and wants the models of the pages it renders created there: an
/// Avalonia object a view model builds (a brush, a resource dictionary) belongs to the thread that created it
/// (<c>AvaloniaObject.Dispatcher</c>), and Avalonia's <c>VerifyAccess</c> refuses it on any other, so a model created on
/// a request's thread cannot be rendered on Avalonia's. The desktop creates its models on Avalonia's thread for the same
/// reason.
/// </summary>
public interface IModelThread
{
    /// <summary>Starts the page's load on the engine's thread, where its models are created and its continuations run, and returns its result.</summary>
    ValueTask<T> LoadAsync<T>(PageDefinition page, Func<ValueTask<T>> load, CancellationToken cancellationToken);
}
