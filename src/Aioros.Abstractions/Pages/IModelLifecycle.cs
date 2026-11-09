namespace Aioros.Pages;

/// <summary>
/// Sees every view model a page creates, for an optional MVVM adapter that maps its toolkit's lifecycle (ReactiveUI's
/// activation, Prism's <c>OnNavigatedTo</c>) onto the page. Registered scoped, it is found in the scope the page's models
/// come from and told of each model once, with the page's request, after the page's models are created and before the
/// page renders. What it starts ends with that scope: after the response on the web, when the page is left on the
/// desktop.
/// </summary>
public interface IModelLifecycle
{
    ValueTask CreatedAsync(object model, PageRequest request, CancellationToken cancellationToken);
}
