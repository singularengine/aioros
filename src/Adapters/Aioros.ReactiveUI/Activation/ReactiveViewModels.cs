namespace Aioros.ReactiveUI.Activation;

/// <summary>
/// What the adapter's generator found in the app's view models, by their shape: <see cref="Attach"/> activates a view
/// model with an <c>Activator</c>, watches one with a <c>Router</c> and shows a routable one, and returns what ends the
/// activation. It is <c>Aioros.Generated.AiorosReactiveViewModels.All</c> in the app.
/// </summary>
public sealed record ReactiveViewModels(Func<object, ReactivePage, IDisposable?> Attach);
