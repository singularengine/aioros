using Aioros.Navigation;

namespace Aioros.Runtime.Pages;

/// <summary>
/// An app's own navigation interface and the generated implementation that navigates through the page's
/// <see cref="INavigator"/>.
/// </summary>
/// <param name="Interface">The interface the app declares and registers with <c>AddNavigation</c>.</param>
/// <param name="Create">Creates the generated implementation over the page's navigator.</param>
public sealed record NavigationInterface(Type Interface, Func<INavigator, object> Create);
